using System.Globalization;
using System.Text.Json;

using Dapr.Client;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Pipeline;
using Hexalith.EventStore.Server.Pipeline.Commands;
using Hexalith.EventStore.Server.Projections;
using Hexalith.EventStore.Server.Tests.TestUtilities;
using Hexalith.EventStore.Testing.Fakes;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;


using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Pipeline;

public class SubmitCommandHandlerIdempotencyAdmissionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Handle_PostCommitActivationFailurePreservesFailureAndMarksProtectedOutcomeUnknown(bool canceled)
    {
        using var cancellation = new CancellationTokenSource();
        Exception original = canceled ? new OperationCanceledException(cancellation.Token) : new InvalidOperationException("re-arm unavailable");
        IProjectionActivationOutbox outbox = Substitute.For<IProjectionActivationOutbox>();
        int writes = 0;
        outbox.EnsureAsync(Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(), Arg.Any<CancellationToken>()).Returns(_ =>
        {
            if (++writes == 1) return Task.CompletedTask;
            if (canceled) cancellation.Cancel();
            return Task.FromException(original);
        });
        using var worker = new ProjectionActivationWorker(outbox, new NoOpProjectionUpdateOrchestrator(),
            Options.Create(new ProjectionDispatchOptions { RetryWorkerInterval = TimeSpan.FromHours(1) }), TimeProvider.System);
        await worker.StartAsync(CancellationToken.None);
        TestContext context = CreateContext(IdempotencyAdmissionDecision.Execute, projectionActivationOutbox: outbox,
            projectionActivationWorker: new ProjectionActivationWorkerCapability(worker, outbox, true));
        context.Router.RouteFencedCommandAsync(Arg.Any<SubmitCommand>(), Arg.Any<IdempotencyExecutionContext>(), cancellation.Token)
            .Returns(new CommandProcessingResult(true, EventCount: 1));

        Exception? observed = null;
        try { await context.Handler.Handle(context.Command, cancellation.Token); }
        catch (Exception exception) { observed = exception; }

        observed.ShouldBeSameAs(original);
        writes.ShouldBe(2);
        await context.Coordinator.Received(1).MarkRecoveryAsync(Arg.Any<IdempotencyAdmissionSession>(),
            IdempotencyAdmissionState.UnknownProviderOutcome, CancellationToken.None);
        await context.Coordinator.DidNotReceiveWithAnyArgs().CompleteAsync(default!, default!, default);
        await context.Router.DidNotReceiveWithAnyArgs().RouteCommandAsync(default!, default);
    }

    [Fact]
    public async Task Handle_WorkerBackedProtectedReplayDoesNotActivateOrRoute()
    {
        IProjectionActivationOutbox outbox = Substitute.For<IProjectionActivationOutbox>();
        using var worker = new ProjectionActivationWorker(outbox, new NoOpProjectionUpdateOrchestrator(),
            Options.Create(new ProjectionDispatchOptions { RetryWorkerInterval = TimeSpan.FromHours(1) }), TimeProvider.System);
        await worker.StartAsync(CancellationToken.None);
        var replay = new CommandProcessingResult(true, EventCount: 1, ResultPayload: "original-result");
        TestContext context = CreateContext(IdempotencyAdmissionDecision.Replay, replay,
            projectionActivationOutbox: outbox, projectionActivationWorker: new ProjectionActivationWorkerCapability(worker, outbox, true));

        SubmitCommandResult returned = await context.Handler.Handle(context.Command, default);

        returned.ResultPayload.ShouldBe(replay.ResultPayload);
        returned.MessageId.ShouldBe(context.Command.MessageId);
        await outbox.DidNotReceiveWithAnyArgs().EnsureAsync(default!, default);
        await context.Router.DidNotReceiveWithAnyArgs().RouteFencedCommandAsync(default!, default!, default);
        await context.Router.DidNotReceiveWithAnyArgs().RouteCommandAsync(default!, default);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Handle_ProtectedExecutionFencesInlineProjectionAndDurableCompletion(bool workerOwnsOutbox)
    {
        using var cancellation = new CancellationTokenSource();
        IProjectionActivationOutbox outbox = Substitute.For<IProjectionActivationOutbox>();
        IProjectionActivationOutbox workerOutbox = workerOwnsOutbox ? outbox : Substitute.For<IProjectionActivationOutbox>();
        using var worker = new ProjectionActivationWorker(workerOutbox, new NoOpProjectionUpdateOrchestrator(),
            Options.Create(new ProjectionDispatchOptions { RetryWorkerInterval = TimeSpan.FromHours(1) }), TimeProvider.System);
        var projection = Substitute.For<IProjectionUpdateOrchestrator>();
        TestContext context = CreateContext(IdempotencyAdmissionDecision.Execute, projectionActivationOutbox: outbox,
            projectionActivationWorker: new ProjectionActivationWorkerCapability(worker, workerOutbox, true),
            projectionOrchestrator: projection);
        var session = (await context.Coordinator.AdmitAsync(context.Command, cancellation.Token))!;
        var execution = context.Command with { IdempotencyKey = null };
        var aggregate = new AggregateIdentity(execution.Tenant, execution.Domain, execution.AggregateId);
        var accepted = new CommandProcessingResult(true, EventCount: 1);
        List<string> calls = [];
        _ = context.Coordinator.BeginAsync(session, cancellation.Token)
            .Returns(_ => { calls.Add("begin"); return Task.CompletedTask; });
        _ = context.Coordinator.ValidateExecutionAsync(session, execution, cancellation.Token)
            .Returns(_ => { calls.Add("fence"); return Task.CompletedTask; });
        _ = outbox.EnsureAsync(aggregate, Arg.Any<CancellationToken>())
            .Returns(_ => { calls.Add("activation"); return Task.CompletedTask; });
        _ = context.Router.RouteFencedCommandAsync(execution, session.ExecutionContext!, cancellation.Token)
            .Returns(_ => { calls.Add("route"); return accepted; });
        _ = projection.UpdateProjectionAsync(aggregate, CancellationToken.None)
            .Returns(_ => { calls.Add("projection"); return Task.CompletedTask; });
        _ = context.Coordinator.CompleteAsync(session, accepted, cancellation.Token)
            .Returns(_ => { calls.Add("complete"); return Task.CompletedTask; });
        await worker.StartAsync(CancellationToken.None);

        try
        {
            SubmitCommandResult result = await context.Handler.Handle(context.Command, cancellation.Token);

            result.MessageId.ShouldBe(context.Command.MessageId);
            calls.ShouldBe(workerOwnsOutbox
                ? ["begin", "fence", "activation", "route", "activation", "fence", "fence", "fence", "complete"]
                : ["begin", "fence", "activation", "route", "activation", "fence", "fence", "fence", "projection", "fence", "complete"]);
            await context.Coordinator.ReceivedWithAnyArgs(workerOwnsOutbox ? 4 : 5).ValidateExecutionAsync(default!, default!, default);
            await projection.ReceivedWithAnyArgs(workerOwnsOutbox ? 0 : 1).UpdateProjectionAsync(default!, default);
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task Handle_OnlyPreProcessingDeferralKeepsSameKeyExecutable()
    {
        TestContext context = CreateContext(IdempotencyAdmissionDecision.Execute);
        var session = (await context.Coordinator.AdmitAsync(context.Command, default))!;
        var retry = session with { Decision = IdempotencyAdmissionDecision.Recoverable };
        context.Coordinator.AdmitAsync(context.Command, default).Returns(session, retry);
        var deferred = new CommandProcessingResult(false) { AdmissionDeferredBeforeProcessing = true };
        context.Router.RouteFencedCommandAsync(Arg.Any<SubmitCommand>(), Arg.Any<IdempotencyExecutionContext>(), Arg.Any<CancellationToken>())
            .Returns(deferred, new CommandProcessingResult(true, EventCount: 1));
        var error = await Should.ThrowAsync<IdempotencyAdmissionFailureException>(() => context.Handler.Handle(context.Command, default));
        error.StatusCode.ShouldBe(503);
        await context.Coordinator.Received(1).MarkRecoveryAsync(Arg.Any<IdempotencyAdmissionSession>(), IdempotencyAdmissionState.Recoverable, default);
        await context.Coordinator.DidNotReceiveWithAnyArgs().CompleteAsync(default!, default!, default);
        var recovered = await context.Handler.Handle(context.Command, default);
        recovered.MessageId.ShouldBe(context.Command.MessageId);
        var originalExecution = context.Command with { IdempotencyKey = null };
        await context.Coordinator.Received().ValidateExecutionAsync(retry, originalExecution, default);
        await context.Router.Received(2).RouteFencedCommandAsync(originalExecution, session.ExecutionContext!, default);
        await context.Coordinator.Received(1).CompleteAsync(Arg.Any<IdempotencyAdmissionSession>(),
            Arg.Is<CommandProcessingResult>(result => result.Accepted && result.EventCount == 1), default);
    }

    [Fact]
    public async Task Handle_RecoverableContenderLosingBeginCannotResetWinningFence()
    {
        TestContext context = CreateContext(IdempotencyAdmissionDecision.Recoverable);
        context.Coordinator.BeginAsync(Arg.Any<IdempotencyAdmissionSession>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("Already pending")));
        await Should.ThrowAsync<InvalidOperationException>(() => context.Handler.Handle(context.Command, default));
        await context.Coordinator.DidNotReceiveWithAnyArgs().MarkRecoveryAsync(default!, default, default);
        await context.Router.DidNotReceiveWithAnyArgs().RouteFencedCommandAsync(default!, default!, default);
    }

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
        await context.Router.DidNotReceive().RouteFencedCommandAsync(
            Arg.Any<SubmitCommand>(),
            Arg.Any<IdempotencyExecutionContext>(),
            Arg.Any<CancellationToken>());
        await context.Router.DidNotReceive().ReconcileFencedCommandAsync(
            Arg.Any<SubmitCommand>(),
            Arg.Any<IdempotencyExecutionContext>(),
            Arg.Any<CancellationToken>());
        await context.Coordinator.DidNotReceive().BeginAsync(
            Arg.Any<IdempotencyAdmissionSession>(),
            Arg.Any<CancellationToken>());
        await context.Coordinator.DidNotReceive().ValidateExecutionAsync(
            Arg.Any<IdempotencyAdmissionSession>(),
            Arg.Any<SubmitCommand>(),
            Arg.Any<CancellationToken>());
        await context.Coordinator.DidNotReceive().CompleteAsync(
            Arg.Any<IdempotencyAdmissionSession>(),
            Arg.Any<CommandProcessingResult>(),
            Arg.Any<CancellationToken>());
        await context.Coordinator.DidNotReceive().MarkRecoveryAsync(
            Arg.Any<IdempotencyAdmissionSession>(),
            Arg.Any<IdempotencyAdmissionState>(),
            Arg.Any<CancellationToken>());
        context.StatusStore.GetStatusHistory(context.Command.Tenant, context.Command.MessageId).ShouldBeEmpty();
        (await context.ArchiveStore.ReadCommandAsync(
            context.Command.Tenant,
            context.Command.MessageId,
            CancellationToken.None)).ShouldBeNull();
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

    [Theory]
    [InlineData(CommandStatus.PublishFailed, false, ResultPayloadOmissionReason.PublishFailed)]
    [InlineData(CommandStatus.Completed, null, null)]
    public async Task Handle_ReplayWithheldResult_UsesOriginalExecutionStatus(
        CommandStatus status, bool? retryable, ResultPayloadOmissionReason? expectedReason)
    {
        const string originalMessageId = "01J22222222222222222222222";
        const string originalCorrelationId = "trace-first";
        TestContext context = CreateContext(IdempotencyAdmissionDecision.Replay,
            new CommandProcessingResult(true, ResultPayloadWithheld: true),
            executionMessageId: originalMessageId, executionCorrelationId: originalCorrelationId);
        await context.StatusStore.WriteStatusAsync(context.Command.Tenant, originalMessageId,
            new CommandStatusRecord(status, DateTimeOffset.UtcNow, context.Command.AggregateId,
                EventCount: 1, RejectionEventType: null, FailureReason: null, TimeoutDuration: null,
                MessageId: originalMessageId, CorrelationId: originalCorrelationId, Retryable: retryable),
            CancellationToken.None);

        SubmitCommandResult result = await context.Handler.Handle(context.Command, CancellationToken.None);

        result.ResultPayload.ShouldBeNull();
        result.ResultPayloadOmissionReason.ShouldBe(expectedReason);
        result.MessageId.ShouldBe(originalMessageId);
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
    public async Task Handle_NewKey_ValidatesBeforeBeginAndFinalizesAfterLastProtectedValidation()
    {
        TestContext context = CreateContext(IdempotencyAdmissionDecision.Execute);
        var calls = new List<string>();
        bool terminal = false;
        _ = context.Coordinator.ValidateExecutionCapabilityAsync(
                Arg.Any<IdempotencyAdmissionSession>(),
                Arg.Any<SubmitCommand>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                terminal.ShouldBeFalse();
                calls.Add("capability");
                return Task.CompletedTask;
            });
        _ = context.Coordinator.BeginAsync(Arg.Any<IdempotencyAdmissionSession>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                calls.ShouldHaveSingleItem().ShouldBe("capability");
                calls.Add("begin");
                return Task.CompletedTask;
            });
        _ = context.Coordinator.ValidateExecutionAsync(
                Arg.Any<IdempotencyAdmissionSession>(),
                Arg.Any<SubmitCommand>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                if (terminal)
                {
                    calls.Add("invalid-post-terminal-validation");
                    throw new InvalidOperationException("Terminal authority must never be validated for later protected work.");
                }

                calls.Add("validate");
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
                terminal.ShouldBeFalse();
                terminal = true;
                calls.Add("complete");
                return Task.CompletedTask;
            });

        _ = await context.Handler.Handle(context.Command, CancellationToken.None);

        calls[0].ShouldBe("capability");
        calls[1].ShouldBe("begin");
        calls.ShouldContain("route");
        calls[^1].ShouldBe("complete");
        calls.ShouldNotContain("invalid-post-terminal-validation");
        terminal.ShouldBeTrue();
    }

    [Fact]
    public async Task Handle_TamperedCapabilityLeavesAdmissionUnchangedBeforeBegin()
    {
        TestContext context = CreateContext(IdempotencyAdmissionDecision.Execute);
        _ = context.Coordinator.ValidateExecutionCapabilityAsync(
                Arg.Any<IdempotencyAdmissionSession>(),
                Arg.Any<SubmitCommand>(),
                Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new InvalidOperationException("tampered capability"));

        _ = await Should.ThrowAsync<InvalidOperationException>(
            () => context.Handler.Handle(context.Command, CancellationToken.None));

        await context.Coordinator.DidNotReceive().BeginAsync(
            Arg.Any<IdempotencyAdmissionSession>(),
            Arg.Any<CancellationToken>());
        await context.Coordinator.DidNotReceive().MarkRecoveryAsync(
            Arg.Any<IdempotencyAdmissionSession>(),
            Arg.Any<IdempotencyAdmissionState>(),
            Arg.Any<CancellationToken>());
        await context.Router.DidNotReceive().RouteFencedCommandAsync(
            Arg.Any<SubmitCommand>(),
            Arg.Any<IdempotencyExecutionContext>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ContextThatLostDurableAuthorityPerformsZeroDownstreamWork()
    {
        TestContext context = CreateContext(IdempotencyAdmissionDecision.Execute);
        _ = context.Coordinator.ValidateExecutionAsync(
                Arg.Any<IdempotencyAdmissionSession>(),
                Arg.Any<SubmitCommand>(),
                Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new InvalidOperationException("authority is terminal"));

        _ = await Should.ThrowAsync<InvalidOperationException>(
            () => context.Handler.Handle(context.Command, CancellationToken.None));

        await context.Coordinator.Received(1).BeginAsync(
            Arg.Any<IdempotencyAdmissionSession>(),
            Arg.Any<CancellationToken>());
        await context.Router.DidNotReceive().RouteFencedCommandAsync(
            Arg.Any<SubmitCommand>(),
            Arg.Any<IdempotencyExecutionContext>(),
            Arg.Any<CancellationToken>());
        await context.Coordinator.DidNotReceive().CompleteAsync(
            Arg.Any<IdempotencyAdmissionSession>(),
            Arg.Any<CommandProcessingResult>(),
            Arg.Any<CancellationToken>());
        context.StatusStore.GetAllStatuses().ShouldBeEmpty();
        context.ArchiveStore.GetAllArchived().ShouldBeEmpty();
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
        result.ResultPayloadOmissionReason.ShouldBe(ResultPayloadOmissionReason.InFlight);
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
    public async Task Handle_LateReceiptOnlyRecovery_RenewsOriginalStatusThroughReplayWindow()
    {
        const string messageId = "01J00000000000000000000001";
        const string originalCorrelation = "trace-first";
        const string statusKey = "tenant-a:01J00000000000000000000001:status";
        TestContext context = CreateContext(
            IdempotencyAdmissionDecision.UnknownProviderOutcome, executionMessageId: messageId,
            executionCorrelationId: originalCorrelation);
        var reconciled = new CommandProcessingResult(true, CorrelationId: originalCorrelation, EventCount: 1);
        _ = context.Router.ReconcileFencedCommandAsync(
            Arg.Any<SubmitCommand>(), Arg.Any<IdempotencyExecutionContext>(), Arg.Any<CancellationToken>())
            .Returns(new IdempotencyCheckResult(IdempotencyCheckOutcome.ExactTerminalDuplicate, reconciled));

        var clock = new FakeTimeProvider();
        var original = new CommandStatusRecord(
            CommandStatus.Completed, clock.GetUtcNow(), context.Command.AggregateId, EventCount: 1,
            RejectionEventType: null, FailureReason: null, TimeoutDuration: null,
            MessageId: messageId, CorrelationId: originalCorrelation)
        {
            Domain = context.Command.Domain,
            CommittedEventSequence = 1,
        };
        DaprClient dapr = Substitute.For<DaprClient>();
        CommandStatusRecord? stored = null;
        DateTimeOffset expiresAt = default;
        bool writeUnavailable = false;
        _ = dapr.SaveStateAsync("statestore", statusKey, Arg.Any<CommandStatusRecord>(),
            stateOptions: Arg.Any<StateOptions?>(), metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
            cancellationToken: Arg.Any<CancellationToken>()).Returns(call =>
        {
            if (writeUnavailable)
            {
                return Task.FromException(new IOException("status store unavailable"));
            }

            int ttl = int.Parse(call.Arg<IReadOnlyDictionary<string, string>>()["ttlInSeconds"], CultureInfo.InvariantCulture);
            expiresAt = clock.GetUtcNow().AddSeconds(ttl);
            stored = call.Arg<CommandStatusRecord>();
            return Task.CompletedTask;
        });
        _ = dapr.GetStateAsync<CommandStatusRecord>("statestore", statusKey,
            consistencyMode: Arg.Any<ConsistencyMode?>(), metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => clock.GetUtcNow() < expiresAt ? stored! : null!);
        var statusStore = new DaprCommandStatusStore(dapr, Options.Create(new CommandStatusOptions { TtlSeconds = 90_000 }),
            NullLogger<DaprCommandStatusStore>.Instance);
        var handler = new SubmitCommandHandler(statusStore, context.ArchiveStore, context.Router,
            NullLogger<SubmitCommandHandler>.Instance, context.Coordinator);

        await statusStore.WriteStatusAsync(context.Command.Tenant, messageId, original);
        DateTimeOffset originalExpiry = expiresAt;
        clock.Advance(TimeSpan.FromHours(2));
        writeUnavailable = true;
        var unavailable = await Should.ThrowAsync<IdempotencyAdmissionFailureException>(
            () => handler.Handle(context.Command, CancellationToken.None));
        unavailable.Code.ShouldBe("idempotency_reconciliation_unavailable");
        unavailable.Retryable.ShouldBeTrue();
        await context.Coordinator.DidNotReceiveWithAnyArgs().CompleteAsync(default!, default!, default);

        writeUnavailable = false;
        DateTimeOffset receiptExpiry = clock.GetUtcNow().AddHours(24);
        SubmitCommandResult recovered = await handler.Handle(context.Command, CancellationToken.None);
        recovered.MessageId.ShouldBe(messageId);
        await context.Coordinator.Received(1).CompleteAsync(Arg.Any<IdempotencyAdmissionSession>(), reconciled, Arg.Any<CancellationToken>());

        IdempotencyAdmissionSession session = await context.Coordinator.AdmitAsync(context.Command, CancellationToken.None);
        _ = context.Coordinator.AdmitAsync(context.Command, Arg.Any<CancellationToken>())
            .Returns(session with { Decision = IdempotencyAdmissionDecision.Replay, ReplayResult = reconciled });
        clock.Advance(TimeSpan.FromHours(23.5));
        clock.GetUtcNow().ShouldBeGreaterThan(originalExpiry);
        clock.GetUtcNow().ShouldBeLessThan(receiptExpiry);

        SubmitCommandResult replay = await handler.Handle(context.Command, CancellationToken.None);
        replay.MessageId.ShouldBe(messageId);
        (await statusStore.ReadStatusAsync(context.Command.Tenant, messageId)).ShouldBe(original);
        await context.Router.DidNotReceiveWithAnyArgs().RouteFencedCommandAsync(default!, default!, default);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("other-domain")]
    public async Task Handle_ReceiptOnlyRecovery_LeavesMissingOrMismatchedStatusRecoverable(string? domain)
    {
        TestContext context = CreateContext(IdempotencyAdmissionDecision.UnknownProviderOutcome);
        _ = context.Router.ReconcileFencedCommandAsync(
            Arg.Any<SubmitCommand>(), Arg.Any<IdempotencyExecutionContext>(), Arg.Any<CancellationToken>())
            .Returns(new IdempotencyCheckResult(IdempotencyCheckOutcome.ExactTerminalDuplicate,
                new CommandProcessingResult(true, EventCount: 1)));
        if (domain is not null)
        {
            await context.StatusStore.WriteStatusAsync(context.Command.Tenant, context.Command.MessageId,
                new CommandStatusRecord(CommandStatus.Completed, DateTimeOffset.UtcNow, context.Command.AggregateId,
                    EventCount: 1, RejectionEventType: null, FailureReason: null, TimeoutDuration: null,
                    MessageId: context.Command.MessageId, CorrelationId: context.Command.CorrelationId) { Domain = domain });
        }

        var failure = await Should.ThrowAsync<IdempotencyAdmissionFailureException>(
            () => context.Handler.Handle(context.Command, CancellationToken.None));

        failure.StatusCode.ShouldBe(503);
        failure.Retryable.ShouldBeTrue();
        context.StatusStore.GetStatusHistory(context.Command.Tenant, context.Command.MessageId).Count.ShouldBe(domain is null ? 0 : 1);
        await context.Coordinator.DidNotReceiveWithAnyArgs().CompleteAsync(default!, default!, default);
        await context.Router.DidNotReceiveWithAnyArgs().RouteFencedCommandAsync(default!, default!, default);
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
    public async Task Handle_PermanentAdmissionRejectionIsNotRetryable()
    {
        TestContext context = CreateContext(IdempotencyAdmissionDecision.Execute);
        _ = context.Coordinator.AdmitAsync(context.Command, Arg.Any<CancellationToken>())
            .Returns<Task<IdempotencyAdmissionSession?>>(_ =>
                throw new ArgumentException("The command payload is not the declared command."));

        IdempotencyAdmissionFailureException exception =
            await Should.ThrowAsync<IdempotencyAdmissionFailureException>(
                () => context.Handler.Handle(context.Command, CancellationToken.None));

        exception.StatusCode.ShouldBe(400);
        exception.Retryable.ShouldBeFalse();
        exception.ClientAction.ShouldBe("correct_request");
        exception.Code.ShouldBe("idempotency_admission_rejected");
        exception.ToString().ShouldNotContain("declared command");
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
        _ = coordinator.ValidateExecutionCapabilityAsync(
                session,
                Arg.Any<SubmitCommand>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
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
        string? executionCorrelationId = null,
        ProjectionActivationWorkerCapability? projectionActivationWorker = null,
        IProjectionUpdateOrchestrator? projectionOrchestrator = null)
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
        _ = coordinator.ValidateExecutionCapabilityAsync(
                session,
                Arg.Any<SubmitCommand>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
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
                projectionOrchestrator: projectionOrchestrator ?? new NoOpProjectionUpdateOrchestrator(),
                logger: logger ?? NullLogger<SubmitCommandHandler>.Instance,
                correlationIndex: null,
                projectionActivationOutbox: projectionActivationOutbox,
                idempotencyAdmissionCoordinator: coordinator,
                projectionActivationWorker: projectionActivationWorker);
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
