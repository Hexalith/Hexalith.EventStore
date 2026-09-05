using System.Reflection;
using System.Text;

using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Pipeline.Commands;

using Microsoft.Extensions.Time.Testing;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Actors;

public class AggregateActorFencingTests
{
    [Fact]
    public async Task ProcessFencedCommandAsync_TamperedFence_RejectsBeforeStateOrDomainAccess()
    {
        IdempotencyExecutionContextProtector protector = CreateProtector();
        ActorTestContext actorContext = AggregateActorTestHelper.CreateActor(
            executionContextProtector: protector);
        CommandEnvelope envelope = AggregateActorTestHelper.CreateTestEnvelope(
            correlationId: "trace-a");
        SubmitCommand command = ToSubmitCommand(envelope);
        IdempotencyExecutionContext executionContext = await protector.ProtectAsync(
            "test-tenant:v1:key-digest",
            7,
            "v1",
            command);

        _ = await Should.ThrowAsync<InvalidOperationException>(
            () => actorContext.Actor.ProcessFencedCommandAsync(
                new FencedCommandEnvelope(
                    envelope,
                    executionContext with { FencingToken = 8 })));

        actorContext.StateManager.ReceivedCalls().ShouldBeEmpty();
        _ = actorContext.Invoker.DidNotReceiveWithAnyArgs()
            .InvokeAsync(default!, default);
    }

    [Fact]
    public async Task ProcessFencedCommandAsync_MissingValidator_RejectsBeforeDomainAccess()
    {
        IdempotencyExecutionContextProtector protector = CreateProtector();
        ActorTestContext actorContext = AggregateActorTestHelper.CreateActor();
        CommandEnvelope envelope = AggregateActorTestHelper.CreateTestEnvelope(
            correlationId: "trace-a");
        IdempotencyExecutionContext executionContext = await protector.ProtectAsync(
            "test-tenant:v1:key-digest",
            7,
            "v1",
            ToSubmitCommand(envelope));

        _ = await Should.ThrowAsync<InvalidOperationException>(
            () => actorContext.Actor.ProcessFencedCommandAsync(
                new FencedCommandEnvelope(envelope, executionContext)));

        _ = actorContext.Invoker.DidNotReceiveWithAnyArgs()
            .InvokeAsync(default!, default);
    }

    [Fact]
    public async Task ProcessFencedCommandAsync_NonCurrentDurableAuthorityRejectsBeforeProtectedWork()
    {
        IIdempotencyAdmissionActor authority = Substitute.For<IIdempotencyAdmissionActor>();
        _ = authority.ValidateAuthorityAsync(Arg.Any<IdempotencyAdmissionAuthorityRequest>())
            .Returns<Task>(_ => throw new InvalidOperationException("terminal authority"));
        IdempotencyExecutionContextProtector protector = CreateProtector(authority);
        ActorTestContext actorContext = AggregateActorTestHelper.CreateActor(
            executionContextProtector: protector);
        CommandEnvelope envelope = AggregateActorTestHelper.CreateTestEnvelope(correlationId: "trace-a");
        IdempotencyExecutionContext executionContext = await protector.ProtectAsync(
            "test-tenant:v1:key-digest",
            7,
            "v1",
            ToSubmitCommand(envelope));

        _ = await Should.ThrowAsync<InvalidOperationException>(
            () => actorContext.Actor.ProcessFencedCommandAsync(
                new FencedCommandEnvelope(envelope, executionContext)));

        await authority.Received(1).ValidateAuthorityAsync(
            Arg.Is<IdempotencyAdmissionAuthorityRequest>(request =>
                request.FencingToken == 7
                && request.Purpose == IdempotencyExecutionPurpose.Execute));
        actorContext.StateManager.ReceivedCalls().ShouldBeEmpty();
        _ = actorContext.Invoker.DidNotReceiveWithAnyArgs().InvokeAsync(default!, default);
    }

    [Fact]
    public async Task ReconcileFencedCommandAsync_ExactResult_ReadsOnlyIdempotencyState()
    {
        IIdempotencyAdmissionActor authority = Substitute.For<IIdempotencyAdmissionActor>();
        _ = authority.ValidateAuthorityAsync(Arg.Is<IdempotencyAdmissionAuthorityRequest>(request =>
                request.FencingToken == 7
                && request.DigestKeyVersion == "v1"
                && request.Purpose == IdempotencyExecutionPurpose.Reconcile))
            .Returns(Task.CompletedTask);
        IdempotencyExecutionContextProtector protector = CreateProtector(authority);
        ActorTestContext actorContext = AggregateActorTestHelper.CreateActor(
            executionContextProtector: protector);
        CommandEnvelope envelope = AggregateActorTestHelper.CreateTestEnvelope(
            correlationId: "trace-a");
        string causationId = envelope.CausationId ?? envelope.MessageId;
        var stored = new IdempotencyRecord(
            causationId,
            envelope.CorrelationId,
            true,
            null,
            DateTimeOffset.UtcNow,
            EventCount: 1,
            MessageId: envelope.MessageId,
            CommandType: envelope.CommandType,
            ExpiresAt: DateTimeOffset.UtcNow.AddHours(1),
            Disposition: IdempotencyRecordDisposition.Terminal);
        _ = actorContext.StateManager.TryGetStateAsync<IdempotencyRecord>(
                $"idempotency:{envelope.MessageId}",
                Arg.Any<CancellationToken>())
            .Returns(new Dapr.Actors.Runtime.ConditionalValue<IdempotencyRecord>(true, stored));
        IdempotencyExecutionContext executionContext = await protector.ProtectAsync(
            "test-tenant:v1:key-digest",
            7,
            "v1",
            ToSubmitCommand(envelope));

        IdempotencyCheckResult result = await actorContext.Actor.ReconcileFencedCommandAsync(
            new FencedCommandEnvelope(envelope, executionContext));

        result.Outcome.ShouldBe(IdempotencyCheckOutcome.ExactTerminalDuplicate);
        result.Result.ShouldBe(stored.ToResult());
        await authority.Received(1).ValidateAuthorityAsync(
            Arg.Is<IdempotencyAdmissionAuthorityRequest>(request =>
                request.FencingToken == 7
                && request.DigestKeyVersion == "v1"
                && request.ExecutionMessageId == envelope.MessageId
                && request.ExecutionCorrelationId == envelope.CorrelationId
                && request.Purpose == IdempotencyExecutionPurpose.Reconcile));
        _ = actorContext.Invoker.DidNotReceiveWithAnyArgs().InvokeAsync(default!, default);
        await actorContext.StateManager.DidNotReceive().SetStateAsync(
            Arg.Any<string>(),
            Arg.Any<object>(),
            Arg.Any<CancellationToken>());
        await actorContext.StateManager.DidNotReceive().SaveStateAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LegacySource_ExactInspectionAndRedirectRetainOriginalEvidenceAndDoNoDomainWork()
    {
        DateTimeOffset processedAt = new(2026, 8, 10, 8, 0, 0, TimeSpan.Zero);
        DateTimeOffset expiresAt = processedAt.AddDays(1);
        var timeProvider = new FakeTimeProvider(processedAt.AddHours(1));
        ActorTestContext actorContext = AggregateActorTestHelper.CreateActor(timeProvider: timeProvider);
        var result = new CommandProcessingResult(
            true,
            CorrelationId: "trace-original",
            EventCount: 1,
            ResultPayload: "protected-result");
        var record = new IdempotencyRecord(
            "01J00000000000000000000000",
            "trace-original",
            true,
            null,
            processedAt,
            EventCount: 1,
            ResultPayload: "protected-result",
            MessageId: "01J00000000000000000000000",
            CommandType: "CreateFolderCommand",
            ExpiresAt: expiresAt,
            Disposition: IdempotencyRecordDisposition.Terminal);
        IdempotencyLegacySourceRedirectRecord? redirect = null;
        _ = actorContext.StateManager.TryGetStateAsync<IdempotencyRecord>(
                "idempotency:01J00000000000000000000000",
                Arg.Any<CancellationToken>())
            .Returns(new Dapr.Actors.Runtime.ConditionalValue<IdempotencyRecord>(true, record));
        _ = actorContext.StateManager.TryGetStateAsync<IdempotencyLegacySourceRedirectRecord>(
                IdempotencyChecker.GetLegacyRedirectKey("01J00000000000000000000000"),
                Arg.Any<CancellationToken>())
            .Returns(_ => redirect is null
                ? new Dapr.Actors.Runtime.ConditionalValue<IdempotencyLegacySourceRedirectRecord>(false, default!)
                : new Dapr.Actors.Runtime.ConditionalValue<IdempotencyLegacySourceRedirectRecord>(true, redirect));
        _ = actorContext.StateManager.SetStateAsync(
                IdempotencyChecker.GetLegacyRedirectKey("01J00000000000000000000000"),
                Arg.Do<IdempotencyLegacySourceRedirectRecord>(value => redirect = value),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var request = new IdempotencyLegacySourceRequest(
            IdempotencyLegacySourceRequest.CurrentSchemaVersion,
            "test-tenant",
            "inventory-2026-08",
            "migration-01J00000000000000000000000",
            1,
            IdempotencyLegacySourceEvidence.Compute(record),
            record.MessageId!,
            record.CorrelationId!,
            processedAt,
            expiresAt,
            result);
        IIdempotencyLegacySourceActor source = actorContext.Actor;

        IdempotencyLegacySourceInspection exact = await source.InspectLegacySourceAsync(request);
        IdempotencyLegacySourceInspection redirected = await source.SetLegacySourceRedirectAsync(
            new IdempotencyLegacySourceRedirectRequest(
                request,
                "test-tenant:v1:key-digest"));
        IdempotencyLegacySourceInspection reproved = await source.InspectLegacySourceAsync(request);

        exact.Decision.ShouldBe(IdempotencyLegacySourceDecision.Exact);
        redirected.Decision.ShouldBe(IdempotencyLegacySourceDecision.Redirected);
        reproved.ShouldBe(redirected);
        redirect.ShouldNotBeNull().TargetAdmissionActorId.ShouldBe("test-tenant:v1:key-digest");
        await actorContext.StateManager.DidNotReceive().TryRemoveStateAsync(
            "idempotency:01J00000000000000000000000",
            Arg.Any<CancellationToken>());
        _ = actorContext.Invoker.DidNotReceiveWithAnyArgs().InvokeAsync(default!, default);
    }

    [Fact]
    public async Task LegacySource_AtAndAfterExpiryIsExpiredAndRemainsReadOnly()
    {
        DateTimeOffset processedAt = new(2026, 8, 10, 8, 0, 0, TimeSpan.Zero);
        DateTimeOffset expiresAt = processedAt.AddDays(1);
        var timeProvider = new FakeTimeProvider(expiresAt);
        ActorTestContext actorContext = AggregateActorTestHelper.CreateActor(timeProvider: timeProvider);
        var record = new IdempotencyRecord(
            "01J00000000000000000000000",
            "trace-original",
            true,
            null,
            processedAt,
            EventCount: 1,
            ResultPayload: "protected-result",
            MessageId: "01J00000000000000000000000",
            CommandType: "CreateFolderCommand",
            ExpiresAt: expiresAt,
            Disposition: IdempotencyRecordDisposition.Terminal);
        _ = actorContext.StateManager.TryGetStateAsync<IdempotencyLegacySourceRedirectRecord>(
                IdempotencyChecker.GetLegacyRedirectKey(record.MessageId!),
                Arg.Any<CancellationToken>())
            .Returns(new Dapr.Actors.Runtime.ConditionalValue<IdempotencyLegacySourceRedirectRecord>(false, default!));
        _ = actorContext.StateManager.TryGetStateAsync<IdempotencyRecord>(
                $"idempotency:{record.MessageId}",
                Arg.Any<CancellationToken>())
            .Returns(new Dapr.Actors.Runtime.ConditionalValue<IdempotencyRecord>(true, record));
        var request = new IdempotencyLegacySourceRequest(
            IdempotencyLegacySourceRequest.CurrentSchemaVersion,
            "test-tenant",
            "inventory-2026-08",
            "migration-01J00000000000000000000000",
            1,
            IdempotencyLegacySourceEvidence.Compute(record),
            record.MessageId!,
            record.CorrelationId!,
            processedAt,
            expiresAt,
            record.ToResult());

        IdempotencyLegacySourceInspection inspection = await ((IIdempotencyLegacySourceActor)actorContext.Actor)
            .InspectLegacySourceAsync(request);

        inspection.Decision.ShouldBe(IdempotencyLegacySourceDecision.Expired);
        timeProvider.SetUtcNow(expiresAt.AddTicks(1));
        IdempotencyLegacySourceInspection afterExpiry = await ((IIdempotencyLegacySourceActor)actorContext.Actor)
            .InspectLegacySourceAsync(request);
        afterExpiry.Decision.ShouldBe(IdempotencyLegacySourceDecision.Expired);
        await actorContext.StateManager.DidNotReceive().SetStateAsync(
            Arg.Any<string>(),
            Arg.Any<object>(),
            Arg.Any<CancellationToken>());
        await actorContext.StateManager.DidNotReceive().TryRemoveStateAsync(
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
        await actorContext.StateManager.DidNotReceive().SaveStateAsync(Arg.Any<CancellationToken>());
        _ = actorContext.Invoker.DidNotReceiveWithAnyArgs().InvokeAsync(default!, default);
    }

    [Fact]
    public async Task LegacySource_UnsupportedShapeRemainsReadOnlyAndFailClosed()
    {
        ActorTestContext actorContext = AggregateActorTestHelper.CreateActor();
        DateTimeOffset processedAt = new(2026, 8, 10, 8, 0, 0, TimeSpan.Zero);
        var unsupported = new IdempotencyRecord(
            "causation",
            "trace-original",
            true,
            null,
            processedAt);
        _ = actorContext.StateManager.TryGetStateAsync<IdempotencyRecord>(
                "idempotency:01J00000000000000000000000",
                Arg.Any<CancellationToken>())
            .Returns(new Dapr.Actors.Runtime.ConditionalValue<IdempotencyRecord>(true, unsupported));
        _ = actorContext.StateManager.TryGetStateAsync<IdempotencyLegacySourceRedirectRecord>(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(new Dapr.Actors.Runtime.ConditionalValue<IdempotencyLegacySourceRedirectRecord>(false, default!));
        var request = new IdempotencyLegacySourceRequest(
            IdempotencyLegacySourceRequest.CurrentSchemaVersion,
            "test-tenant",
            "inventory-2026-08",
            "migration-01J00000000000000000000000",
            1,
            IdempotencyLegacySourceEvidence.Compute(unsupported),
            "01J00000000000000000000000",
            "trace-original",
            processedAt,
            processedAt.AddDays(1),
            unsupported.ToResult());

        IdempotencyLegacySourceInspection inspection = await ((IIdempotencyLegacySourceActor)actorContext.Actor)
            .InspectLegacySourceAsync(request);

        inspection.Decision.ShouldBe(IdempotencyLegacySourceDecision.Unsupported);
        await actorContext.StateManager.DidNotReceive().SetStateAsync(
            Arg.Any<string>(),
            Arg.Any<object>(),
            Arg.Any<CancellationToken>());
        await actorContext.StateManager.DidNotReceive().SaveStateAsync(Arg.Any<CancellationToken>());
        _ = actorContext.Invoker.DidNotReceiveWithAnyArgs().InvokeAsync(default!, default);
    }

    [Fact]
    public async Task LegacySource_OneProtectedResultMismatchIsReadOnlyConflict()
    {
        ActorTestContext actorContext = AggregateActorTestHelper.CreateActor();
        DateTimeOffset processedAt = new(2026, 8, 10, 8, 0, 0, TimeSpan.Zero);
        DateTimeOffset expiresAt = processedAt.AddDays(1);
        var record = new IdempotencyRecord(
            "01J00000000000000000000000",
            "trace-original",
            true,
            null,
            processedAt,
            EventCount: 1,
            ResultPayload: "protected-result",
            MessageId: "01J00000000000000000000000",
            CommandType: "CreateFolderCommand",
            ExpiresAt: expiresAt,
            Disposition: IdempotencyRecordDisposition.Terminal);
        _ = actorContext.StateManager.TryGetStateAsync<IdempotencyLegacySourceRedirectRecord>(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(new Dapr.Actors.Runtime.ConditionalValue<IdempotencyLegacySourceRedirectRecord>(false, default!));
        _ = actorContext.StateManager.TryGetStateAsync<IdempotencyRecord>(
                "idempotency:01J00000000000000000000000",
                Arg.Any<CancellationToken>())
            .Returns(new Dapr.Actors.Runtime.ConditionalValue<IdempotencyRecord>(true, record));
        var request = new IdempotencyLegacySourceRequest(
            IdempotencyLegacySourceRequest.CurrentSchemaVersion,
            "test-tenant",
            "inventory-2026-08",
            "migration-01J00000000000000000000000",
            1,
            IdempotencyLegacySourceEvidence.Compute(record),
            record.MessageId!,
            record.CorrelationId!,
            processedAt,
            expiresAt,
            record.ToResult() with { ResultPayload = "mismatched-result" });

        IdempotencyLegacySourceInspection inspection = await ((IIdempotencyLegacySourceActor)actorContext.Actor)
            .InspectLegacySourceAsync(request);

        inspection.Decision.ShouldBe(IdempotencyLegacySourceDecision.Conflict);
        await actorContext.StateManager.DidNotReceive().SetStateAsync(
            Arg.Any<string>(),
            Arg.Any<object>(),
            Arg.Any<CancellationToken>());
        await actorContext.StateManager.DidNotReceive().SaveStateAsync(Arg.Any<CancellationToken>());
        _ = actorContext.Invoker.DidNotReceiveWithAnyArgs().InvokeAsync(default!, default);
    }

    [Fact]
    public async Task ProcessCommandAsync_PersistedLegacyRedirectRejectsBeforeDomainEventOrPipelineWork()
    {
        ActorTestContext actorContext = AggregateActorTestHelper.CreateActor();
        CommandEnvelope command = AggregateActorTestHelper.CreateTestEnvelope(correlationId: "trace-redirected");
        var redirect = new IdempotencyLegacySourceRedirectRecord(
            IdempotencyLegacySourceRedirectRecord.CurrentSchemaVersion,
            command.TenantId,
            "inventory-2026-08",
            "migration-redirected",
            "source-evidence",
            "test-tenant:v2:target");
        _ = actorContext.StateManager.TryGetStateAsync<IdempotencyLegacySourceRedirectRecord>(
                IdempotencyChecker.GetLegacyRedirectKey(command.MessageId),
                Arg.Any<CancellationToken>())
            .Returns(new Dapr.Actors.Runtime.ConditionalValue<IdempotencyLegacySourceRedirectRecord>(true, redirect));

        CommandProcessingResult result = await actorContext.Actor.ProcessCommandAsync(command);

        result.Accepted.ShouldBeFalse();
        result.ErrorMessage.ShouldBe("idempotency_legacy_redirected");
        _ = actorContext.Invoker.DidNotReceiveWithAnyArgs().InvokeAsync(default!, default);
        await actorContext.StateManager.DidNotReceive().SetStateAsync(
            Arg.Any<string>(),
            Arg.Any<object>(),
            Arg.Any<CancellationToken>());
        await actorContext.StateManager.DidNotReceive().SaveStateAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LegacySource_StateUnavailableReturnsBoundedDecisionWithoutLeakingStoreError()
    {
        const string RawStateKeySentinel = "idempotency:raw-state-key-must-not-leak";
        ActorTestContext actorContext = AggregateActorTestHelper.CreateActor();
        _ = actorContext.StateManager.TryGetStateAsync<IdempotencyLegacySourceRedirectRecord>(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns<Task<Dapr.Actors.Runtime.ConditionalValue<IdempotencyLegacySourceRedirectRecord>>>(_ =>
                throw new InvalidOperationException(RawStateKeySentinel));
        DateTimeOffset consumedAt = new(2026, 8, 10, 8, 0, 0, TimeSpan.Zero);
        var request = new IdempotencyLegacySourceRequest(
            IdempotencyLegacySourceRequest.CurrentSchemaVersion,
            "test-tenant",
            "inventory-2026-08",
            "migration-01J00000000000000000000000",
            1,
            "source-evidence-digest",
            "01J00000000000000000000000",
            "trace-original",
            consumedAt,
            consumedAt.AddDays(1),
            new CommandProcessingResult(true, CorrelationId: "trace-original"));

        IdempotencyLegacySourceInspection inspection = await ((IIdempotencyLegacySourceActor)actorContext.Actor)
            .InspectLegacySourceAsync(request);

        inspection.Decision.ShouldBe(IdempotencyLegacySourceDecision.Unavailable);
        inspection.ToString().ShouldNotContain(RawStateKeySentinel);
        _ = actorContext.Invoker.DidNotReceiveWithAnyArgs().InvokeAsync(default!, default);
    }

    [Fact]
    public async Task LegacyRedirectSaveCommitsThenThrows_ReturnsRedirectedFromFreshDurableWitness()
    {
        var stateManager = new FaultInjectingActorStateManager();
        (IdempotencyRecord record, IdempotencyLegacySourceRequest source,
            IdempotencyLegacySourceRedirectRequest redirect) = CreateLegacyRedirectFixture();
        await stateManager.SeedCommittedStateAsync(new Dictionary<string, object>
        {
            [$"idempotency:{record.MessageId}"] = record,
        });
        stateManager.FaultAfterCall("SaveState", 1, new InvalidOperationException("commit uncertain"));
        ActorTestContext actorContext = AggregateActorTestHelper.CreateActor(stateManager: stateManager);

        IdempotencyLegacySourceInspection result = await ((IIdempotencyLegacySourceActor)actorContext.Actor)
            .SetLegacySourceRedirectAsync(redirect);

        result.Decision.ShouldBe(IdempotencyLegacySourceDecision.Redirected);
        stateManager.CommittedState.ShouldContainKey(
            IdempotencyChecker.GetLegacyRedirectKey(source.ExecutionMessageId));
        stateManager.Trace.Count(operation => operation == "SaveState").ShouldBe(1);
    }

    [Fact]
    public async Task LegacyRedirectSaveFailsBeforeCommit_ReturnsUnavailableAndDiscardsStagedRedirect()
    {
        var stateManager = new FaultInjectingActorStateManager();
        (IdempotencyRecord record, IdempotencyLegacySourceRequest source,
            IdempotencyLegacySourceRedirectRequest redirect) = CreateLegacyRedirectFixture();
        await stateManager.SeedCommittedStateAsync(new Dictionary<string, object>
        {
            [$"idempotency:{record.MessageId}"] = record,
        });
        stateManager.FaultOnCall("SaveState", 1, new InvalidOperationException("pre-commit failure"));
        ActorTestContext actorContext = AggregateActorTestHelper.CreateActor(stateManager: stateManager);
        IIdempotencyLegacySourceActor actor = actorContext.Actor;

        IdempotencyLegacySourceInspection result = await actor.SetLegacySourceRedirectAsync(redirect);
        IdempotencyLegacySourceInspection reproved = await actor.InspectLegacySourceAsync(source);

        result.Decision.ShouldBe(IdempotencyLegacySourceDecision.Unavailable);
        reproved.Decision.ShouldBe(IdempotencyLegacySourceDecision.Exact);
        stateManager.CommittedState.ShouldNotContainKey(
            IdempotencyChecker.GetLegacyRedirectKey(source.ExecutionMessageId));
    }

    [Fact]
    public async Task PoisonedActor_ReconciliationStopsAtTheCacheBarrierBeforeStateInspection()
    {
        var stateManager = new FaultInjectingActorStateManager();
        stateManager.FaultOnCall("ClearCache", 1, new IOException("cache unavailable"));
        IdempotencyExecutionContextProtector protector = CreateProtector();
        ActorTestContext actorContext = AggregateActorTestHelper.CreateActor(
            stateManager: stateManager,
            executionContextProtector: protector);
        CommandEnvelope command = AggregateActorTestHelper.CreateTestEnvelope(
            correlationId: "corr-poison-reconcile");
        IdempotencyExecutionContext executionContext = await protector.ProtectAsync(
            "test-tenant:v1:key-digest",
            7,
            "v1",
            ToSubmitCommand(command));
        Poison(actorContext.Actor);

        _ = await Should.ThrowAsync<ActorStateRemediationException>(
            () => actorContext.Actor.ReconcileFencedCommandAsync(
                new FencedCommandEnvelope(command, executionContext)));

        stateManager.Trace.ShouldBe(["ClearCache"]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PoisonedActor_LegacyInspectionAndRedirectStopAtTheCacheBarrier(bool redirectTurn)
    {
        var stateManager = new FaultInjectingActorStateManager();
        stateManager.FaultOnCall("ClearCache", 1, new IOException("cache unavailable"));
        ActorTestContext actorContext = AggregateActorTestHelper.CreateActor(stateManager: stateManager);
        (_, IdempotencyLegacySourceRequest source, IdempotencyLegacySourceRedirectRequest redirect) =
            CreateLegacyRedirectFixture();
        IIdempotencyLegacySourceActor actor = actorContext.Actor;
        Poison(actorContext.Actor);

        _ = await Should.ThrowAsync<ActorStateRemediationException>(() => redirectTurn
            ? actor.SetLegacySourceRedirectAsync(redirect)
            : actor.InspectLegacySourceAsync(source));

        stateManager.Trace.ShouldBe(["ClearCache"]);
    }

    private static (
        IdempotencyRecord Record,
        IdempotencyLegacySourceRequest Source,
        IdempotencyLegacySourceRedirectRequest Redirect) CreateLegacyRedirectFixture()
    {
        DateTimeOffset processedAt = new(2026, 8, 10, 8, 0, 0, TimeSpan.Zero);
        DateTimeOffset expiresAt = DateTimeOffset.UtcNow.AddDays(1);
        var record = new IdempotencyRecord(
            "legacy-message",
            "legacy-correlation",
            true,
            null,
            processedAt,
            EventCount: 1,
            MessageId: "legacy-message",
            CommandType: "CreateFolderCommand",
            ExpiresAt: expiresAt,
            Disposition: IdempotencyRecordDisposition.Terminal);
        var source = new IdempotencyLegacySourceRequest(
            IdempotencyLegacySourceRequest.CurrentSchemaVersion,
            "test-tenant",
            "inventory-2026-08",
            "migration-legacy-message",
            1,
            IdempotencyLegacySourceEvidence.Compute(record),
            record.MessageId!,
            record.CorrelationId!,
            processedAt,
            expiresAt,
            record.ToResult());
        return (
            record,
            source,
            new IdempotencyLegacySourceRedirectRequest(source, "test-tenant:v1:key-digest"));
    }

    private static IdempotencyExecutionContextProtector CreateProtector(
        IIdempotencyAdmissionActor? authority = null)
    {
        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        bool configureDefaultAuthority = authority is null;
        authority ??= Substitute.For<IIdempotencyAdmissionActor>();
        _ = factory.CreateActorProxy<IIdempotencyAdmissionActor>(
                Arg.Any<ActorId>(),
                IdempotencyAdmissionActor.ActorTypeName)
            .Returns(authority);
        if (configureDefaultAuthority)
        {
            _ = authority.ValidateAuthorityAsync(Arg.Any<IdempotencyAdmissionAuthorityRequest>())
                .Returns(Task.CompletedTask);
        }
        return new IdempotencyExecutionContextProtector(
            new StaticIdempotencyDigestKeyProvider(
                "v1",
                new Dictionary<string, byte[]>(StringComparer.Ordinal)
                {
                    ["v1"] = Encoding.UTF8.GetBytes("0123456789abcdef0123456789abcdef"),
                },
                []),
            factory);
    }

    private static SubmitCommand ToSubmitCommand(CommandEnvelope envelope)
        => new(
            envelope.MessageId,
            envelope.TenantId,
            envelope.Domain,
            envelope.AggregateId,
            envelope.CommandType,
            envelope.Payload,
            envelope.CorrelationId,
            envelope.UserId,
            envelope.Extensions);

    private static void Poison(AggregateActor actor)
        => typeof(AggregateActor)
            .GetField("_stateCacheUnsafe", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(actor, true);
}
