using System.Text.Json;

using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Pipeline;
using Hexalith.EventStore.Server.Pipeline.Commands;
using Hexalith.EventStore.Server.Projections;
using Hexalith.EventStore.Server.Tests.TestUtilities;
using Hexalith.EventStore.Testing.Fakes;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Pipeline;

public class SubmitCommandHandlerIdempotencyAdmissionTests
{
    [Fact]
    public async Task Handle_Conflict_DeniesBeforeAggregateAndAdvisoryStores()
    {
        TestContext context = CreateContext(IdempotencyAdmissionDecision.Conflict);

        IdempotencyConflictException exception = await Should.ThrowAsync<IdempotencyConflictException>(
            () => context.Handler.Handle(context.Command, CancellationToken.None));

        exception.ToString().ShouldNotContain(context.Command.IdempotencyKey!);

        await context.Router.DidNotReceive().RouteCommandAsync(
            Arg.Any<SubmitCommand>(),
            Arg.Any<CancellationToken>());
        context.StatusStore.GetStatusHistory(context.Command.Tenant, context.Command.MessageId).ShouldBeEmpty();
        (await context.ArchiveStore.ReadCommandAsync(
            context.Command.Tenant,
            context.Command.MessageId,
            CancellationToken.None)).ShouldBeNull();
    }

    [Fact]
    public async Task Handle_Expired_DeniesBeforeAggregateAndAdvisoryStores()
    {
        TestContext context = CreateContext(IdempotencyAdmissionDecision.Expired);

        IdempotencyKeyExpiredException exception = await Should.ThrowAsync<IdempotencyKeyExpiredException>(
            () => context.Handler.Handle(context.Command, CancellationToken.None));

        exception.ToString().ShouldNotContain(context.Command.IdempotencyKey!);

        await context.Router.DidNotReceive().RouteCommandAsync(
            Arg.Any<SubmitCommand>(),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(IdempotencyAdmissionDecision.Corrupt, "idempotency_admission_corrupt")]
    [InlineData(IdempotencyAdmissionDecision.Collision, "idempotency_key_collision")]
    [InlineData(IdempotencyAdmissionDecision.UnsafeLegacy, "idempotency_unsafe_legacy_state")]
    public async Task Handle_UnverifiableAdmission_ReturnsStableFailClosedOutcome(
        IdempotencyAdmissionDecision decision,
        string expectedCode)
    {
        TestContext context = CreateContext(decision);

        IdempotencyAdmissionFailureException exception =
            await Should.ThrowAsync<IdempotencyAdmissionFailureException>(
                () => context.Handler.Handle(context.Command, CancellationToken.None));

        exception.Code.ShouldBe(expectedCode);
        exception.Retryable.ShouldBeFalse();
        exception.ToString().ShouldNotContain(context.Command.IdempotencyKey!);
        await context.Router.DidNotReceive().RouteCommandAsync(
            Arg.Any<SubmitCommand>(),
            Arg.Any<CancellationToken>());
        await context.Router.DidNotReceive().RouteFencedCommandAsync(
            Arg.Any<SubmitCommand>(),
            Arg.Any<IdempotencyExecutionContext>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Replay_ReturnsStoredResultWithoutSecondSideEffect()
    {
        var replay = new CommandProcessingResult(
            true,
            CorrelationId: "original-correlation",
            EventCount: 2,
            ResultPayload: "{\"status\":\"same\"}");
        TestContext context = CreateContext(IdempotencyAdmissionDecision.Replay, replay);

        SubmitCommandResult result = await context.Handler.Handle(context.Command, CancellationToken.None);

        result.CorrelationId.ShouldBe(context.Command.CorrelationId);
        result.ResultPayload.ShouldBe(replay.ResultPayload);
        await context.Router.DidNotReceive().RouteCommandAsync(
            Arg.Any<SubmitCommand>(),
            Arg.Any<CancellationToken>());
        context.StatusStore.GetStatusHistory(context.Command.Tenant, context.Command.MessageId).ShouldBeEmpty();
        (await context.ArchiveStore.ReadCommandAsync(
            context.Command.Tenant,
            context.Command.MessageId,
            CancellationToken.None)).ShouldBeNull();
    }

    [Fact]
    public async Task Handle_ReplayWithheldSuccess_PreservesOriginalPayloadDecision()
    {
        var replay = new CommandProcessingResult(
            true,
            CorrelationId: "original-correlation",
            EventCount: 2,
            ResultPayload: "{\"protected\":true}",
            ResultPayloadWithheld: true);
        TestContext context = CreateContext(IdempotencyAdmissionDecision.Replay, replay);

        SubmitCommandResult result = await context.Handler.Handle(context.Command, CancellationToken.None);

        result.ResultPayload.ShouldBeNull();
        await context.Router.DidNotReceive().RouteFencedCommandAsync(
            Arg.Any<SubmitCommand>(),
            Arg.Any<IdempotencyExecutionContext>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ReplayDomainRejection_RehydratesTypedExceptionWithoutAdvisoryRead()
    {
        var replay = new CommandProcessingResult(
            false,
            ErrorMessage: "Domain rejection: FolderAlreadyExists",
            CorrelationId: "original-correlation",
            RejectionEventType: "FolderAlreadyExists",
            FailureReason: "DomainRejected",
            ResultPayloadWithheld: true);
        TestContext context = CreateContext(IdempotencyAdmissionDecision.Replay, replay);

        DomainCommandRejectedException exception = await Should.ThrowAsync<DomainCommandRejectedException>(
            () => context.Handler.Handle(context.Command, CancellationToken.None));

        exception.CorrelationId.ShouldBe(context.Command.CorrelationId);
        exception.RejectionType.ShouldBe("FolderAlreadyExists");
        context.StatusStore.GetAllStatuses().ShouldBeEmpty();
    }

    [Fact]
    public async Task Handle_ReplayBackpressure_RehydratesTypedException()
    {
        var replay = new CommandProcessingResult(
            false,
            ErrorMessage: "Backpressure exceeded",
            CorrelationId: "original-correlation",
            BackpressureExceeded: true,
            BackpressurePendingCount: 9,
            BackpressureThreshold: 8,
            FailureReason: "BackpressureExceeded",
            ResultPayloadWithheld: true);
        TestContext context = CreateContext(IdempotencyAdmissionDecision.Replay, replay);

        Hexalith.EventStore.Server.Actors.BackpressureExceededException exception =
            await Should.ThrowAsync<Hexalith.EventStore.Server.Actors.BackpressureExceededException>(
                () => context.Handler.Handle(context.Command, CancellationToken.None));

        exception.CorrelationId.ShouldBe(context.Command.CorrelationId);
        exception.PendingCount.ShouldBe(9);
        exception.Threshold.ShouldBe(8);
    }

    [Fact]
    public async Task Handle_NewKey_BeginsBeforeRouteWithOriginalMessageIdentityAndFinalizesReturnedResult()
    {
        TestContext context = CreateContext(IdempotencyAdmissionDecision.Execute);
        var calls = new List<string>();
        _ = context.Coordinator.BeginAsync(Arg.Any<IdempotencyAdmissionSession>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                calls.Add("begin");
                return Task.CompletedTask;
            });
        _ = context.Router.RouteFencedCommandAsync(
                Arg.Any<SubmitCommand>(),
                Arg.Any<IdempotencyExecutionContext>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                SubmitCommand routed = callInfo.ArgAt<SubmitCommand>(0);
                routed.MessageId.ShouldBe(context.Command.MessageId);
                routed.IdempotencyKey.ShouldBeNull();
                calls.Add("route");
                return new CommandProcessingResult(true, CorrelationId: context.Command.CorrelationId);
            });
        _ = context.Coordinator.CompleteAsync(
                Arg.Any<IdempotencyAdmissionSession>(),
                Arg.Any<CommandProcessingResult>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                calls.Add("complete");
                return Task.CompletedTask;
            });

        _ = await context.Handler.Handle(context.Command, CancellationToken.None);

        calls.ShouldBe(["begin", "route", "complete"]);
    }

    [Fact]
    public async Task Handle_PendingEquivalent_ReturnsFirstWriterTaskEvidenceWithoutDownstreamWork()
    {
        const string firstMessageId = "01J22222222222222222222222";
        TestContext context = CreateContext(
            IdempotencyAdmissionDecision.Pending,
            executionMessageId: firstMessageId,
            executionCorrelationId: "trace-first");

        SubmitCommandResult result = await context.Handler.Handle(context.Command, CancellationToken.None);

        result.CorrelationId.ShouldBe(context.Command.CorrelationId);
        result.MessageId.ShouldBe(firstMessageId);
        await context.Coordinator.DidNotReceive().BeginAsync(
            Arg.Any<IdempotencyAdmissionSession>(),
            Arg.Any<CancellationToken>());
        await context.Router.DidNotReceive().RouteCommandAsync(
            Arg.Any<SubmitCommand>(),
            Arg.Any<CancellationToken>());
        await context.Router.DidNotReceive().RouteFencedCommandAsync(
            Arg.Any<SubmitCommand>(),
            Arg.Any<IdempotencyExecutionContext>(),
            Arg.Any<CancellationToken>());
        context.StatusStore.GetAllStatuses().ShouldBeEmpty();
        context.ArchiveStore.GetAllArchived().ShouldBeEmpty();
    }

    [Fact]
    public async Task Handle_Recoverable_RoutesPersistedIdentityUnderExistingFence()
    {
        const string firstMessageId = "01J22222222222222222222222";
        const string firstCorrelationId = "trace-first";
        TestContext context = CreateContext(
            IdempotencyAdmissionDecision.Recoverable,
            executionMessageId: firstMessageId,
            executionCorrelationId: firstCorrelationId);

        SubmitCommandResult result = await context.Handler.Handle(context.Command, CancellationToken.None);

        result.MessageId.ShouldBe(firstMessageId);
        result.CorrelationId.ShouldBe(context.Command.CorrelationId);
        await context.Coordinator.Received(1).BeginAsync(
            Arg.Is<IdempotencyAdmissionSession>(session => session.FencingToken == 3),
            Arg.Any<CancellationToken>());
        await context.Router.Received(1).RouteFencedCommandAsync(
            Arg.Is<SubmitCommand>(command =>
                command.MessageId == firstMessageId
                && command.CorrelationId == firstCorrelationId
                && command.IdempotencyKey == null),
            Arg.Is<IdempotencyExecutionContext>(fence =>
                fence.FencingToken == 3
                && fence.MessageId == firstMessageId
                && fence.CorrelationId == firstCorrelationId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UnknownOutcome_ReconcilesReadOnlyAndFinalizesExactAggregateResult()
    {
        var reconciled = new CommandProcessingResult(
            true,
            CorrelationId: "trace-first",
            EventCount: 1,
            ResultPayload: "{\"status\":\"same\"}");
        TestContext context = CreateContext(IdempotencyAdmissionDecision.UnknownProviderOutcome);
        _ = context.Router.ReconcileFencedCommandAsync(
                Arg.Any<SubmitCommand>(),
                Arg.Any<IdempotencyExecutionContext>(),
                Arg.Any<CancellationToken>())
            .Returns(new IdempotencyCheckResult(
                IdempotencyCheckOutcome.ExactTerminalDuplicate,
                reconciled));

        SubmitCommandResult result = await context.Handler.Handle(context.Command, CancellationToken.None);

        result.CorrelationId.ShouldBe(context.Command.CorrelationId);
        result.ResultPayload.ShouldBe(reconciled.ResultPayload);
        await context.Coordinator.Received(1).CompleteAsync(
            Arg.Any<IdempotencyAdmissionSession>(),
            reconciled,
            Arg.Any<CancellationToken>());
        await context.Coordinator.DidNotReceive().BeginAsync(
            Arg.Any<IdempotencyAdmissionSession>(),
            Arg.Any<CancellationToken>());
        await context.Router.DidNotReceive().RouteFencedCommandAsync(
            Arg.Any<SubmitCommand>(),
            Arg.Any<IdempotencyExecutionContext>(),
            Arg.Any<CancellationToken>());
        context.StatusStore.GetAllStatuses().ShouldBeEmpty();
        context.ArchiveStore.GetAllArchived().ShouldBeEmpty();
    }

    [Fact]
    public async Task Handle_UnknownOutcomeWithoutAuthoritativeResult_RemainsFailClosed()
    {
        TestContext context = CreateContext(IdempotencyAdmissionDecision.UnknownProviderOutcome);

        _ = await Should.ThrowAsync<InvalidOperationException>(
            () => context.Handler.Handle(context.Command, CancellationToken.None));

        await context.Router.Received(1).ReconcileFencedCommandAsync(
            Arg.Any<SubmitCommand>(),
            Arg.Any<IdempotencyExecutionContext>(),
            Arg.Any<CancellationToken>());
        await context.Coordinator.DidNotReceive().CompleteAsync(
            Arg.Any<IdempotencyAdmissionSession>(),
            Arg.Any<CommandProcessingResult>(),
            Arg.Any<CancellationToken>());
        await context.Router.DidNotReceive().RouteFencedCommandAsync(
            Arg.Any<SubmitCommand>(),
            Arg.Any<IdempotencyExecutionContext>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_RouteFailure_MarksUnknownOutcomeUnderSameFence()
    {
        TestContext context = CreateContext(IdempotencyAdmissionDecision.Execute);
        _ = context.Router.RouteFencedCommandAsync(
                Arg.Any<SubmitCommand>(),
                Arg.Any<IdempotencyExecutionContext>(),
                Arg.Any<CancellationToken>())
            .Returns<Task<CommandProcessingResult>>(_ => throw new InvalidOperationException("route failed"));

        _ = await Should.ThrowAsync<InvalidOperationException>(
            () => context.Handler.Handle(context.Command, CancellationToken.None));

        await context.Coordinator.Received(1).MarkRecoveryAsync(
            Arg.Any<IdempotencyAdmissionSession>(),
            IdempotencyAdmissionState.UnknownProviderOutcome,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WriteAheadFailureAfterFenceMarksUnknownOutcome()
    {
        IProjectionActivationOutbox outbox = Substitute.For<IProjectionActivationOutbox>();
        _ = outbox.EnsureAsync(Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new InvalidOperationException("outbox unavailable"));
        TestContext context = CreateContext(
            IdempotencyAdmissionDecision.Execute,
            projectionActivationOutbox: outbox);

        _ = await Should.ThrowAsync<InvalidOperationException>(
            () => context.Handler.Handle(context.Command, CancellationToken.None));

        await context.Coordinator.Received(1).BeginAsync(
            Arg.Any<IdempotencyAdmissionSession>(),
            Arg.Any<CancellationToken>());
        await context.Coordinator.Received(1).MarkRecoveryAsync(
            Arg.Any<IdempotencyAdmissionSession>(),
            IdempotencyAdmissionState.UnknownProviderOutcome,
            Arg.Any<CancellationToken>());
        await context.Router.DidNotReceive().RouteCommandAsync(
            Arg.Any<SubmitCommand>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NewKey_BeginsFenceBeforeOutboxAndRoutesOnlyFencedCommand()
    {
        var calls = new List<string>();
        IProjectionActivationOutbox outbox = Substitute.For<IProjectionActivationOutbox>();
        TestContext context = CreateContext(
            IdempotencyAdmissionDecision.Execute,
            projectionActivationOutbox: outbox,
            executionContext: ExecutionContext());
        _ = context.Coordinator.BeginAsync(Arg.Any<IdempotencyAdmissionSession>(), Arg.Any<CancellationToken>())
            .Returns(_ => { calls.Add("begin"); return Task.CompletedTask; });
        _ = outbox.EnsureAsync(Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(), Arg.Any<CancellationToken>())
            .Returns(_ => { calls.Add("outbox"); return Task.CompletedTask; });
        _ = context.Router.RouteFencedCommandAsync(
                Arg.Any<SubmitCommand>(),
                Arg.Any<IdempotencyExecutionContext>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                calls.Add("route");
                return new CommandProcessingResult(true, CorrelationId: context.Command.CorrelationId);
            });

        _ = await context.Handler.Handle(context.Command, CancellationToken.None);

        calls.ShouldBe(["begin", "outbox", "route"]);
        await context.Router.DidNotReceive().RouteCommandAsync(
            Arg.Any<SubmitCommand>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_AdmissionStoreUnavailableFailsClosedBeforeRoute()
    {
        TestContext context = CreateContext(IdempotencyAdmissionDecision.Execute);
        _ = context.Coordinator.AdmitAsync(context.Command, Arg.Any<CancellationToken>())
            .Returns<Task<IdempotencyAdmissionSession?>>(_ =>
                throw new InvalidOperationException("admission unavailable"));

        _ = await Should.ThrowAsync<InvalidOperationException>(
            () => context.Handler.Handle(context.Command, CancellationToken.None));

        await context.Router.DidNotReceive().RouteCommandAsync(
            Arg.Any<SubmitCommand>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_TrustedAdmissionDoesNotLogRawIdempotencyKey()
    {
        var entries = new List<Hexalith.EventStore.Server.Tests.TestUtilities.LogEntry>();
        TestContext context = CreateContext(
            IdempotencyAdmissionDecision.Execute,
            logger: new TestLogger<SubmitCommandHandler>(entries));

        _ = await context.Handler.Handle(context.Command, CancellationToken.None);

        entries.ShouldNotBeEmpty();
        entries.ShouldAllBe(entry =>
            !entry.Message.Contains(context.Command.IdempotencyKey!, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Handle_RealOpaqueKey_DoesNotCrossExecutionOrAdvisoryPersistenceBoundaries()
    {
        var entries = new List<Hexalith.EventStore.Server.Tests.TestUtilities.LogEntry>();
        var capturedSurfaces = new List<string>();
        var statusStore = new InMemoryCommandStatusStore();
        var archiveStore = new InMemoryCommandArchiveStore();
        ICommandRouter router = Substitute.For<ICommandRouter>();
        ICommandCorrelationIndex correlationIndex = Substitute.For<ICommandCorrelationIndex>();
        ICommandActivityTracker activityTracker = Substitute.For<ICommandActivityTracker>();
        IIdempotencyAdmissionCoordinator coordinator = Substitute.For<IIdempotencyAdmissionCoordinator>();
        SubmitCommand command = CreateCommand();
        var session = new IdempotencyAdmissionSession(
            "tenant-a:v1:digest",
            3,
            IdempotencyAdmissionDecision.Execute,
            ExecutionContext: ExecutionContext(),
            ExecutionMessageId: command.MessageId,
            ExecutionCorrelationId: command.CorrelationId);

        _ = coordinator.AdmitAsync(command, Arg.Any<CancellationToken>()).Returns(session);
        _ = coordinator.BeginAsync(session, Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        _ = coordinator.CompleteAsync(session, Arg.Any<CommandProcessingResult>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _ = router.RouteFencedCommandAsync(
                Arg.Any<SubmitCommand>(),
                Arg.Any<IdempotencyExecutionContext>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                SubmitCommand routed = callInfo.ArgAt<SubmitCommand>(0);
                capturedSurfaces.Add(JsonSerializer.Serialize(routed));
                capturedSurfaces.Add(JsonSerializer.Serialize(routed.ToCommandEnvelope()));
                return new CommandProcessingResult(true, command.CorrelationId);
            });
        _ = correlationIndex.AddAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedSurfaces.Add(string.Join('|',
                    callInfo.ArgAt<string>(0),
                    callInfo.ArgAt<string>(1),
                    callInfo.ArgAt<string>(2)));
                return CommandCorrelationIndexAddOutcome.Added;
            });
        _ = activityTracker.TrackAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<Hexalith.EventStore.Contracts.Commands.CommandStatus>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<int?>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedSurfaces.Add(string.Join('|',
                    callInfo.ArgAt<string>(0),
                    callInfo.ArgAt<string>(1),
                    callInfo.ArgAt<string>(2),
                    callInfo.ArgAt<string>(3),
                    callInfo.ArgAt<string>(4)));
                return Task.CompletedTask;
            });
        var handler = new SubmitCommandHandler(
            statusStore,
            archiveStore,
            router,
            activityTracker,
            streamActivityTracker: null,
            projectionOrchestrator: new NoOpProjectionUpdateOrchestrator(),
            logger: new TestLogger<SubmitCommandHandler>(entries),
            correlationIndex: correlationIndex,
            projectionActivationOutbox: null,
            idempotencyAdmissionCoordinator: coordinator);

        SubmitCommandResult result = await handler.Handle(command, CancellationToken.None);

        capturedSurfaces.Add(JsonSerializer.Serialize(result));
        capturedSurfaces.Add(JsonSerializer.Serialize(statusStore.GetAllStatuses()));
        capturedSurfaces.Add(JsonSerializer.Serialize(archiveStore.GetAllArchived()));
        capturedSurfaces.AddRange(entries.Select(entry => entry.Message));
        capturedSurfaces.ShouldAllBe(surface =>
            !surface.Contains(command.IdempotencyKey!, StringComparison.Ordinal));
        await router.Received(1).RouteFencedCommandAsync(
            Arg.Is<SubmitCommand>(routed =>
                routed.MessageId == command.MessageId
                && routed.IdempotencyKey == null),
            Arg.Any<IdempotencyExecutionContext>(),
            Arg.Any<CancellationToken>());
    }

    private static TestContext CreateContext(
        IdempotencyAdmissionDecision decision,
        CommandProcessingResult? replay = null,
        Microsoft.Extensions.Logging.ILogger<SubmitCommandHandler>? logger = null,
        IProjectionActivationOutbox? projectionActivationOutbox = null,
        IdempotencyExecutionContext? executionContext = null,
        string? executionMessageId = null,
        string? executionCorrelationId = null)
    {
        var statusStore = new InMemoryCommandStatusStore();
        var archiveStore = new InMemoryCommandArchiveStore();
        ICommandRouter router = Substitute.For<ICommandRouter>();
        IIdempotencyAdmissionCoordinator coordinator = Substitute.For<IIdempotencyAdmissionCoordinator>();
        SubmitCommand command = CreateCommand();
        executionMessageId ??= command.MessageId;
        executionCorrelationId ??= command.CorrelationId;
        var session = new IdempotencyAdmissionSession(
            "tenant-a:v1:digest",
            3,
            decision,
            replay,
            executionContext ?? (decision is IdempotencyAdmissionDecision.Execute
                or IdempotencyAdmissionDecision.Recoverable
                or IdempotencyAdmissionDecision.UnknownProviderOutcome
                ? ExecutionContext(executionMessageId, executionCorrelationId)
                : null),
            executionMessageId,
            executionCorrelationId);
        _ = coordinator.AdmitAsync(command, Arg.Any<CancellationToken>())
            .Returns(session);
        _ = coordinator.BeginAsync(session, Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        _ = coordinator.CompleteAsync(
                session,
                Arg.Any<CommandProcessingResult>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _ = coordinator.MarkRecoveryAsync(
                session,
                Arg.Any<IdempotencyAdmissionState>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _ = router.RouteCommandAsync(Arg.Any<SubmitCommand>(), Arg.Any<CancellationToken>())
            .Returns(new CommandProcessingResult(true, CorrelationId: command.CorrelationId));
        _ = router.RouteFencedCommandAsync(
                Arg.Any<SubmitCommand>(),
                Arg.Any<IdempotencyExecutionContext>(),
                Arg.Any<CancellationToken>())
            .Returns(new CommandProcessingResult(true, CorrelationId: command.CorrelationId));
        _ = router.ReconcileFencedCommandAsync(
                Arg.Any<SubmitCommand>(),
                Arg.Any<IdempotencyExecutionContext>(),
                Arg.Any<CancellationToken>())
            .Returns(new IdempotencyCheckResult(IdempotencyCheckOutcome.Miss));
        var handler = projectionActivationOutbox is null
            ? new SubmitCommandHandler(
                statusStore,
                archiveStore,
                router,
                logger ?? NullLogger<SubmitCommandHandler>.Instance,
                coordinator)
            : new SubmitCommandHandler(
                statusStore,
                archiveStore,
                router,
                activityTracker: null,
                streamActivityTracker: null,
                projectionOrchestrator: new NoOpProjectionUpdateOrchestrator(),
                logger: logger ?? NullLogger<SubmitCommandHandler>.Instance,
                correlationIndex: null,
                projectionActivationOutbox: projectionActivationOutbox,
                idempotencyAdmissionCoordinator: coordinator);
        return new TestContext(handler, statusStore, archiveStore, router, coordinator, command);
    }

    private static SubmitCommand CreateCommand()
        => new(
            MessageId: "01J00000000000000000000000",
            Tenant: "tenant-a",
            Domain: "folders",
            AggregateId: "folder-a",
            CommandType: "CreateFolderCommand",
            Payload: [1],
            CorrelationId: "trace-current",
            UserId: "user-a",
            IdempotencyKey: "opaque-key-that-must-not-leak");

    private static IdempotencyExecutionContext ExecutionContext(
        string messageId = "01J00000000000000000000000",
        string correlationId = "trace-current")
        => new(
            IdempotencyExecutionContext.CurrentSchemaVersion,
            "tenant-a:v1:digest",
            3,
            "v1",
            messageId,
            correlationId,
            "tenant-a",
            "folders",
            "folder-a",
            "CreateFolderCommand",
            "proof");

    private sealed record TestContext(
        SubmitCommandHandler Handler,
        InMemoryCommandStatusStore StatusStore,
        InMemoryCommandArchiveStore ArchiveStore,
        ICommandRouter Router,
        IIdempotencyAdmissionCoordinator Coordinator,
        SubmitCommand Command);
}
