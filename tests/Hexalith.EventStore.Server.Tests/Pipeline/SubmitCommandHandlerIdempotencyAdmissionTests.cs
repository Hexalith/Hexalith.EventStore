using System.Text;

using Hexalith.EventStore.Contracts.Commands;
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

        _ = await Should.ThrowAsync<IdempotencyConflictException>(
            () => context.Handler.Handle(context.Command, CancellationToken.None));

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

        _ = await Should.ThrowAsync<IdempotencyKeyExpiredException>(
            () => context.Handler.Handle(context.Command, CancellationToken.None));

        await context.Router.DidNotReceive().RouteCommandAsync(
            Arg.Any<SubmitCommand>(),
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
    public async Task Handle_NewKey_BeginsBeforeRouteAndFinalizesReturnedResult()
    {
        TestContext context = CreateContext(IdempotencyAdmissionDecision.Execute);
        var calls = new List<string>();
        _ = context.Coordinator.BeginAsync(Arg.Any<IdempotencyAdmissionSession>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                calls.Add("begin");
                return Task.CompletedTask;
            });
        _ = context.Router.RouteCommandAsync(Arg.Any<SubmitCommand>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                callInfo.ArgAt<SubmitCommand>(0).MessageId.ShouldBe("downstream-execution-id");
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
    public async Task Handle_RouteFailure_MarksUnknownOutcomeUnderSameFence()
    {
        TestContext context = CreateContext(IdempotencyAdmissionDecision.Execute);
        _ = context.Router.RouteCommandAsync(Arg.Any<SubmitCommand>(), Arg.Any<CancellationToken>())
            .Returns<Task<CommandProcessingResult>>(_ => throw new InvalidOperationException("route failed"));

        _ = await Should.ThrowAsync<InvalidOperationException>(
            () => context.Handler.Handle(context.Command, CancellationToken.None));

        await context.Coordinator.Received(1).MarkRecoveryAsync(
            Arg.Any<IdempotencyAdmissionSession>(),
            IdempotencyAdmissionState.UnknownProviderOutcome,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WriteAheadFailureMarksRecoverableBeforeSideEffectBoundary()
    {
        IProjectionActivationOutbox outbox = Substitute.For<IProjectionActivationOutbox>();
        _ = outbox.EnsureAsync(Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new InvalidOperationException("outbox unavailable"));
        TestContext context = CreateContext(
            IdempotencyAdmissionDecision.Execute,
            projectionActivationOutbox: outbox);

        _ = await Should.ThrowAsync<InvalidOperationException>(
            () => context.Handler.Handle(context.Command, CancellationToken.None));

        await context.Coordinator.DidNotReceive().BeginAsync(
            Arg.Any<IdempotencyAdmissionSession>(),
            Arg.Any<CancellationToken>());
        await context.Coordinator.Received(1).MarkRecoveryAsync(
            Arg.Any<IdempotencyAdmissionSession>(),
            IdempotencyAdmissionState.Recoverable,
            Arg.Any<CancellationToken>());
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
            !entry.Message.Contains(context.Command.MessageId, StringComparison.Ordinal));
    }

    private static TestContext CreateContext(
        IdempotencyAdmissionDecision decision,
        CommandProcessingResult? replay = null,
        Microsoft.Extensions.Logging.ILogger<SubmitCommandHandler>? logger = null,
        IProjectionActivationOutbox? projectionActivationOutbox = null)
    {
        var statusStore = new InMemoryCommandStatusStore();
        var archiveStore = new InMemoryCommandArchiveStore();
        ICommandRouter router = Substitute.For<ICommandRouter>();
        IIdempotencyAdmissionCoordinator coordinator = Substitute.For<IIdempotencyAdmissionCoordinator>();
        SubmitCommand command = CreateCommand();
        var session = new IdempotencyAdmissionSession(
            "tenant-a:v1:digest",
            3,
            decision,
            replay,
            decision == IdempotencyAdmissionDecision.Execute ? "downstream-execution-id" : null);
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
            MessageId: "opaque-key-that-must-not-leak",
            Tenant: "tenant-a",
            Domain: "folders",
            AggregateId: "folder-a",
            CommandType: "CreateFolderCommand",
            Payload: [1],
            CorrelationId: "trace-current",
            UserId: "user-a",
            Idempotency: new CanonicalIdempotencyDescriptor(
                "folders",
                "CreateFolder",
                1,
                Encoding.UTF8.GetBytes("canonical-intent"),
                IdempotencyReplayRetentionTier.Mutation));

    private sealed record TestContext(
        SubmitCommandHandler Handler,
        InMemoryCommandStatusStore StatusStore,
        InMemoryCommandArchiveStore ArchiveStore,
        ICommandRouter Router,
        IIdempotencyAdmissionCoordinator Coordinator,
        SubmitCommand Command);
}
