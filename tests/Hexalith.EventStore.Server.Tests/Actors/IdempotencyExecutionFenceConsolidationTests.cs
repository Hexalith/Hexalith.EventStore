using System.Text;

using Dapr.Actors;
using Dapr.Actors.Client;
using Dapr.Actors.Runtime;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Pipeline;
using Hexalith.EventStore.Server.Pipeline.Commands;
using Hexalith.EventStore.Server.Projections;
using Hexalith.EventStore.Server.Tests.TestUtilities;
using Hexalith.EventStore.Testing.Fakes;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Actors;

/// <summary>Validates atomic BeginAsync, single pre-advisory fencing, and atomic CompleteAsync under stale-owner, concurrency, and recovery conditions.</summary>
public sealed class IdempotencyExecutionFenceConsolidationTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 28, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Atomic BeginAsync rejects stale fencing tokens, redirects, unactivated promotions, mismatched identity, and tampered capabilities without corrupting actor state.</summary>
    [Theory]
    [InlineData("stale-token")]
    [InlineData("redirect")]
    [InlineData("unactivated-promotion")]
    [InlineData("wrong-message-id")]
    [InlineData("wrong-key-version")]
    [InlineData("already-terminal")]
    [InlineData("tampered-proof")]
    public async Task AtomicBeginAsync_RejectsStaleOrInvalidAuthorityFailClosed(string failure)
    {
        (IdempotencyAdmissionActor actor, StateHolder state, IdempotencyAdmissionCoordinator coordinator, IdempotencyExecutionContextProtector protector) = CreateHarness();
        SubmitCommand execution = CreateExecutionCommand();
        state.Record = CreateRecord(
            state: failure == "already-terminal" ? IdempotencyAdmissionState.Terminal : IdempotencyAdmissionState.Reserved,
            fencingToken: failure == "stale-token" ? 9 : 7,
            digestKeyVersion: failure == "wrong-key-version" ? "v2" : "v1",
            executionMessageId: failure == "wrong-message-id" ? "01J99999999999999999999999" : execution.MessageId,
            replayResult: failure == "already-terminal" ? new CommandProcessingResult(true) : null);
        if (failure == "redirect")
        {
            state.Redirect = new IdempotencyAdmissionRedirectRecord(
                IdempotencyAdmissionRedirectRecord.CurrentSchemaVersion,
                "tenant-a:v2:key-digest");
        }

        if (failure == "unactivated-promotion")
        {
            string digest = IdempotencyAdmissionPromotionEvidence.Compute(state.Record, null);
            state.Promotion = new IdempotencyAdmissionPromotionRecord(
                IdempotencyAdmissionPromotionRecord.CurrentSchemaVersion,
                "tenant-a:v0:key-digest",
                Activated: false,
                "migration-1",
                digest,
                digest,
                digest);
        }

        IdempotencyExecutionContext context = await protector.ProtectAsync(
            "tenant-a:v1:key-digest",
            fencingToken: 7,
            digestKeyVersion: "v1",
            execution);
        if (failure == "tampered-proof")
        {
            context = context with { Proof = "tampered-proof-value" };
        }

        var session = new IdempotencyAdmissionSession(
            "tenant-a:v1:key-digest",
            7,
            IdempotencyAdmissionDecision.Execute,
            ExecutionContext: context,
            ExecutionMessageId: execution.MessageId,
            ExecutionCorrelationId: execution.CorrelationId);
        IdempotencyAdmissionState initialState = state.Record.State;

        Exception error = await Should.ThrowAsync<InvalidOperationException>(
            () => coordinator.BeginAsync(session, execution, TestContext.Current.CancellationToken));

        if (failure == "tampered-proof")
        {
            error.ShouldBeOfType<IdempotencyCapabilityValidationException>();
        }

        state.Record.State.ShouldBe(initialState);
    }

    /// <summary>When two Recoverable contenders race atomic BeginAsync, exactly one transitions to Pending and the loser cannot reset the winner's fence.</summary>
    [Fact]
    public async Task AtomicBeginAsync_ConcurrentRecoverableRaceAllowsSingleWinnerAndProtectsWinnerFence()
    {
        (IdempotencyAdmissionActor actor, StateHolder state, IdempotencyAdmissionCoordinator coordinator, IdempotencyExecutionContextProtector protector) = CreateHarness();
        SubmitCommand command = CreateCommand();
        SubmitCommand execution = command with { IdempotencyKey = null };
        state.Record = CreateRecord(state: IdempotencyAdmissionState.Recoverable, fencingToken: 7);
        IdempotencyExecutionContext context = await protector.ProtectAsync("tenant-a:v1:key-digest", 7, "v1", execution);
        var session = new IdempotencyAdmissionSession(
            "tenant-a:v1:key-digest",
            7,
            IdempotencyAdmissionDecision.Recoverable,
            ExecutionContext: context,
            ExecutionMessageId: execution.MessageId,
            ExecutionCorrelationId: execution.CorrelationId);

        // Winner transitions Recoverable -> Pending atomically.
        await coordinator.BeginAsync(session, execution, TestContext.Current.CancellationToken);
        state.Record.State.ShouldBe(IdempotencyAdmissionState.Pending);

        // Loser racing through SubmitCommandHandler with the same Recoverable session fails atomic BeginAsync
        // and must NOT invoke MarkRecoveryAsync to revert the winner's Pending state.
        IIdempotencyAdmissionCoordinator handlerCoordinator = Substitute.For<IIdempotencyAdmissionCoordinator>();
        _ = handlerCoordinator.AdmitAsync(command, Arg.Any<CancellationToken>()).Returns(session);
        _ = handlerCoordinator.BeginAsync(session, execution, Arg.Any<CancellationToken>())
            .Returns(call => coordinator.BeginAsync(
                call.ArgAt<IdempotencyAdmissionSession>(0),
                call.ArgAt<SubmitCommand>(1),
                call.ArgAt<CancellationToken>(2)));
        ICommandRouter router = Substitute.For<ICommandRouter>();
        var handler = new SubmitCommandHandler(
            new InMemoryCommandStatusStore(),
            new InMemoryCommandArchiveStore(),
            router,
            NullLogger<SubmitCommandHandler>.Instance,
            handlerCoordinator);

        _ = await Should.ThrowAsync<InvalidOperationException>(() => handler.Handle(command, TestContext.Current.CancellationToken));

        state.Record.State.ShouldBe(IdempotencyAdmissionState.Pending);
        await handlerCoordinator.DidNotReceiveWithAnyArgs().MarkRecoveryAsync(default!, default, default);
        await router.DidNotReceiveWithAnyArgs().RouteFencedCommandAsync(default!, default!, default);
    }

    /// <summary>When ownership is lost after routing, the single pre-advisory fence fails fast before any advisory store write and marks UnknownProviderOutcome.</summary>
    [Fact]
    public async Task SinglePreAdvisoryFence_StaleOwnerFailsFastBeforeAnyAdvisoryWriteAndMarksUnknownOutcome()
    {
        var statusStore = new InMemoryCommandStatusStore();
        var archiveStore = new InMemoryCommandArchiveStore();
        ICommandRouter router = Substitute.For<ICommandRouter>();
        ICommandCorrelationIndex correlationIndex = Substitute.For<ICommandCorrelationIndex>();
        ICommandActivityTracker activityTracker = Substitute.For<ICommandActivityTracker>();
        IStreamActivityTracker streamActivityTracker = Substitute.For<IStreamActivityTracker>();
        IIdempotencyAdmissionCoordinator coordinator = Substitute.For<IIdempotencyAdmissionCoordinator>();
        SubmitCommand command = CreateCommand();
        SubmitCommand execution = command with { IdempotencyKey = null };
        var executionContext = new IdempotencyExecutionContext(
            IdempotencyExecutionContext.CurrentSchemaVersion,
            "tenant-a:v1:key-digest",
            7,
            "v1",
            execution.MessageId,
            execution.CorrelationId,
            execution.Tenant,
            execution.Domain,
            execution.AggregateId,
            execution.CommandType,
            "proof");
        var session = new IdempotencyAdmissionSession(
            "tenant-a:v1:key-digest",
            7,
            IdempotencyAdmissionDecision.Execute,
            ExecutionContext: executionContext,
            ExecutionMessageId: execution.MessageId,
            ExecutionCorrelationId: execution.CorrelationId);
        _ = coordinator.AdmitAsync(command, Arg.Any<CancellationToken>()).Returns(session);
        _ = coordinator.BeginAsync(session, execution, Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        _ = router.RouteFencedCommandAsync(execution, executionContext, Arg.Any<CancellationToken>())
            .Returns(new CommandProcessingResult(true, CorrelationId: execution.CorrelationId, EventCount: 1));
        _ = coordinator.ValidateExecutionAsync(session, execution, Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("The idempotency execution authority is no longer current.")));
        var handler = new SubmitCommandHandler(
            statusStore,
            archiveStore,
            router,
            activityTracker,
            streamActivityTracker,
            new NoOpProjectionUpdateOrchestrator(),
            NullLogger<SubmitCommandHandler>.Instance,
            correlationIndex: correlationIndex,
            projectionActivationOutbox: null,
            idempotencyAdmissionCoordinator: coordinator);

        _ = await Should.ThrowAsync<InvalidOperationException>(() => handler.Handle(command, TestContext.Current.CancellationToken));

        await coordinator.Received(1).ValidateExecutionAsync(session, execution, TestContext.Current.CancellationToken);
        await coordinator.Received(1).MarkRecoveryAsync(session, IdempotencyAdmissionState.UnknownProviderOutcome, CancellationToken.None);
        await coordinator.DidNotReceiveWithAnyArgs().CompleteAsync(default!, default!, default!, default);
        statusStore.GetAllStatuses().ShouldBeEmpty();
        archiveStore.GetAllArchived().ShouldBeEmpty();
        await correlationIndex.DidNotReceiveWithAnyArgs().AddAsync(default!, default!, default!, default);
        await activityTracker.DidNotReceiveWithAnyArgs().TrackAsync(default!, default!, default!, default!, default!, default, default, default, default, default);
        await streamActivityTracker.DidNotReceiveWithAnyArgs().TrackAsync(default!, default!, default!, default, default, default);
    }

    /// <summary>Atomic CompleteAsync rejects stale fences, redirects, unactivated promotions, wrong execution purpose, and tampered proofs without finalizing.</summary>
    [Theory]
    [InlineData("stale-token")]
    [InlineData("redirect")]
    [InlineData("unactivated-promotion")]
    [InlineData("execute-from-unknown-outcome")]
    [InlineData("reconcile-from-pending")]
    [InlineData("tampered-proof")]
    public async Task AtomicCompleteAsync_RejectsStaleOrWrongPurposeAuthorityFailClosed(string failure)
    {
        (_, StateHolder state, IdempotencyAdmissionCoordinator coordinator, IdempotencyExecutionContextProtector protector) = CreateHarness();
        SubmitCommand execution = CreateExecutionCommand();
        IdempotencyAdmissionState storedState = failure == "execute-from-unknown-outcome"
            ? IdempotencyAdmissionState.UnknownProviderOutcome
            : IdempotencyAdmissionState.Pending;
        state.Record = CreateRecord(
            state: storedState,
            fencingToken: failure == "stale-token" ? 9 : 7);
        if (failure == "redirect")
        {
            state.Redirect = new IdempotencyAdmissionRedirectRecord(
                IdempotencyAdmissionRedirectRecord.CurrentSchemaVersion,
                "tenant-a:v2:key-digest");
        }

        if (failure == "unactivated-promotion")
        {
            string digest = IdempotencyAdmissionPromotionEvidence.Compute(state.Record, null);
            state.Promotion = new IdempotencyAdmissionPromotionRecord(
                IdempotencyAdmissionPromotionRecord.CurrentSchemaVersion,
                "tenant-a:v0:key-digest",
                Activated: false,
                "migration-1",
                digest,
                digest,
                digest);
        }

        IdempotencyExecutionContext context = await protector.ProtectAsync(
            "tenant-a:v1:key-digest",
            fencingToken: 7,
            digestKeyVersion: "v1",
            execution);
        if (failure == "tampered-proof")
        {
            context = context with { Proof = "tampered-proof-value" };
        }

        IdempotencyAdmissionDecision decision = failure == "reconcile-from-pending"
            ? IdempotencyAdmissionDecision.UnknownProviderOutcome
            : IdempotencyAdmissionDecision.Execute;
        var session = new IdempotencyAdmissionSession(
            "tenant-a:v1:key-digest",
            7,
            decision,
            ExecutionContext: context,
            ExecutionMessageId: execution.MessageId,
            ExecutionCorrelationId: execution.CorrelationId);

        _ = await Should.ThrowAsync<InvalidOperationException>(() =>
            coordinator.CompleteAsync(session, execution, new CommandProcessingResult(true, EventCount: 1), TestContext.Current.CancellationToken));

        state.Record.State.ShouldBe(storedState);
        state.Record.ReplayResult.ShouldBeNull();
    }

    /// <summary>After a post-routing crash marks UnknownProviderOutcome, reconciliation validates authority and atomically finalizes Terminal replay.</summary>
    [Fact]
    public async Task CrashRecovery_UnknownOutcomeReconciliationValidatesAndCompletesAtomically()
    {
        (IdempotencyAdmissionActor actor, StateHolder state, IdempotencyAdmissionCoordinator coordinator, IdempotencyExecutionContextProtector protector) = CreateHarness();
        SubmitCommand execution = CreateExecutionCommand();
        var request = new IdempotencyAdmissionRequest(
            IdempotencyAdmissionRecord.CurrentSchemaVersion,
            "tenant-a",
            "v1",
            "key-digest",
            "verification-tag",
            "intent-a",
            IdempotencyReplayRetentionTier.Mutation,
            execution.MessageId,
            execution.CorrelationId);

        IdempotencyAdmissionResult initial = await actor.AdmitAsync(request);
        initial.Decision.ShouldBe(IdempotencyAdmissionDecision.Execute);
        IdempotencyExecutionContext context = await protector.ProtectAsync(
            "tenant-a:v1:key-digest",
            initial.FencingToken,
            "v1",
            execution);
        var executeSession = new IdempotencyAdmissionSession(
            "tenant-a:v1:key-digest",
            initial.FencingToken,
            IdempotencyAdmissionDecision.Execute,
            ExecutionContext: context,
            ExecutionMessageId: execution.MessageId,
            ExecutionCorrelationId: execution.CorrelationId);

        await coordinator.BeginAsync(executeSession, execution, TestContext.Current.CancellationToken);
        state.Record!.State.ShouldBe(IdempotencyAdmissionState.Pending);

        // Simulate crash after side-effect boundary crossed.
        await coordinator.MarkRecoveryAsync(executeSession, IdempotencyAdmissionState.UnknownProviderOutcome, TestContext.Current.CancellationToken);
        state.Record.State.ShouldBe(IdempotencyAdmissionState.UnknownProviderOutcome);

        // Retry classifies UnknownProviderOutcome and reconciles under exact reconciliation authority.
        IdempotencyAdmissionResult retry = await actor.AdmitAsync(request);
        retry.Decision.ShouldBe(IdempotencyAdmissionDecision.UnknownProviderOutcome);
        var reconcileSession = executeSession with { Decision = IdempotencyAdmissionDecision.UnknownProviderOutcome };
        await coordinator.ValidateExecutionAsync(reconcileSession, execution, TestContext.Current.CancellationToken);

        var reconciledResult = new CommandProcessingResult(true, CorrelationId: execution.CorrelationId, EventCount: 1, ResultPayload: "{\"ok\":true}");
        await coordinator.CompleteAsync(reconcileSession, execution, reconciledResult, TestContext.Current.CancellationToken);
        state.Record.State.ShouldBe(IdempotencyAdmissionState.Terminal);
        state.Record.ReplayResult.ShouldBe(reconciledResult);

        // Next retry returns deterministic Replay.
        IdempotencyAdmissionResult replay = await actor.AdmitAsync(request);
        replay.Decision.ShouldBe(IdempotencyAdmissionDecision.Replay);
        replay.ReplayResult.ShouldBe(reconciledResult);
    }

    private static (IdempotencyAdmissionActor Actor, StateHolder State, IdempotencyAdmissionCoordinator Coordinator, IdempotencyExecutionContextProtector Protector) CreateHarness()
    {
        var state = new StateHolder();
        IActorStateManager stateManager = Substitute.For<IActorStateManager>();
        _ = stateManager.TryGetStateAsync<IdempotencyAdmissionRecord>(IdempotencyAdmissionActor.StateName, Arg.Any<CancellationToken>())
            .Returns(_ => state.Record is { } value
                ? new ConditionalValue<IdempotencyAdmissionRecord>(true, value)
                : new ConditionalValue<IdempotencyAdmissionRecord>(false, default!));
        _ = stateManager.TryGetStateAsync<IdempotencyAdmissionTombstone>(IdempotencyAdmissionActor.TombstoneStateName, Arg.Any<CancellationToken>())
            .Returns(_ => state.Tombstone is { } value
                ? new ConditionalValue<IdempotencyAdmissionTombstone>(true, value)
                : new ConditionalValue<IdempotencyAdmissionTombstone>(false, default!));
        _ = stateManager.TryGetStateAsync<IdempotencyAdmissionPromotionRecord>(IdempotencyAdmissionActor.PromotionStateName, Arg.Any<CancellationToken>())
            .Returns(_ => state.Promotion is { } value
                ? new ConditionalValue<IdempotencyAdmissionPromotionRecord>(true, value)
                : new ConditionalValue<IdempotencyAdmissionPromotionRecord>(false, default!));
        _ = stateManager.TryGetStateAsync<IdempotencyAdmissionRedirectRecord>(IdempotencyAdmissionActor.RedirectStateName, Arg.Any<CancellationToken>())
            .Returns(_ => state.Redirect is { } value
                ? new ConditionalValue<IdempotencyAdmissionRedirectRecord>(true, value)
                : new ConditionalValue<IdempotencyAdmissionRedirectRecord>(false, default!));
        _ = stateManager.SetStateAsync(IdempotencyAdmissionActor.StateName, Arg.Do<IdempotencyAdmissionRecord>(value => state.Record = value), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _ = stateManager.SetStateAsync(IdempotencyAdmissionActor.TombstoneStateName, Arg.Do<IdempotencyAdmissionTombstone>(value => state.Tombstone = value), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _ = stateManager.SetStateAsync(IdempotencyAdmissionActor.PromotionStateName, Arg.Do<IdempotencyAdmissionPromotionRecord>(value => state.Promotion = value), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _ = stateManager.TryRemoveStateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                switch (callInfo.ArgAt<string>(0))
                {
                    case IdempotencyAdmissionActor.StateName:
                        state.Record = null;
                        break;
                    case IdempotencyAdmissionActor.TombstoneStateName:
                        state.Tombstone = null;
                        break;
                    case IdempotencyAdmissionActor.PromotionStateName:
                        state.Promotion = null;
                        break;
                }

                return true;
            });

        ActorTimerManager timerManager = Substitute.For<ActorTimerManager>();
        var clock = new FakeTimeProvider(_now);
        ActorHost host = ActorHost.CreateForTest<IdempotencyAdmissionActor>(
            new ActorTestOptions
            {
                ActorId = new ActorId("tenant-a:v1:key-digest"),
                TimerManager = timerManager,
            });
        var actor = new IdempotencyAdmissionActor(host, NullLogger<IdempotencyAdmissionActor>.Instance, clock);
        ActorStateManagerTestHelper.SetStateManager(actor, stateManager);

        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        _ = factory.CreateActorProxy<IIdempotencyAdmissionActor>(Arg.Any<ActorId>(), IdempotencyAdmissionActor.ActorTypeName)
            .Returns(actor);
        var keyProvider = new StaticIdempotencyDigestKeyProvider(
            "v1",
            new Dictionary<string, byte[]>(StringComparer.Ordinal)
            {
                ["v1"] = Encoding.UTF8.GetBytes("0123456789abcdef0123456789abcdef"),
            },
            []);
        var protector = new IdempotencyExecutionContextProtector(keyProvider, factory);
        var coordinator = new IdempotencyAdmissionCoordinator(
            factory,
            new IdempotencyKeyProtector(keyProvider),
            Substitute.For<IIdempotencyIntentAdapterRegistry>(),
            protector);
        return (actor, state, coordinator, protector);
    }

    private static SubmitCommand CreateCommand()
        => new(
            MessageId: "01J00000000000000000000000",
            Tenant: "tenant-a",
            Domain: "folders",
            AggregateId: "folder-a",
            CommandType: "CreateFolderCommand",
            Payload: [1],
            CorrelationId: "trace-original",
            UserId: "user-a",
            IdempotencyKey: "opaque-key");

    private static SubmitCommand CreateExecutionCommand()
        => CreateCommand() with { IdempotencyKey = null };

    private static IdempotencyAdmissionRecord CreateRecord(
        IdempotencyAdmissionState state = IdempotencyAdmissionState.Reserved,
        long fencingToken = 7,
        string digestKeyVersion = "v1",
        string executionMessageId = "01J00000000000000000000000",
        CommandProcessingResult? replayResult = null)
        => new(
            IdempotencyAdmissionRecord.CurrentSchemaVersion,
            state,
            "tenant-a",
            digestKeyVersion,
            "key-digest",
            "verification-tag",
            "intent-a",
            IdempotencyReplayRetentionTier.Mutation,
            _now.AddMinutes(-5),
            _now,
            state == IdempotencyAdmissionState.Terminal ? _now.AddHours(1) : null,
            fencingToken,
            replayResult,
            executionMessageId,
            "trace-original");

    private sealed class StateHolder
    {
        public IdempotencyAdmissionRecord? Record { get; set; }

        public IdempotencyAdmissionTombstone? Tombstone { get; set; }

        public IdempotencyAdmissionPromotionRecord? Promotion { get; set; }

        public IdempotencyAdmissionRedirectRecord? Redirect { get; set; }
    }
}
