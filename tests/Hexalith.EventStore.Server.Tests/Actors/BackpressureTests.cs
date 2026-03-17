
using System.Reflection;

using Dapr.Actors;
using Dapr.Actors.Runtime;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.Server.Events;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Shouldly;

using EventEnvelope = Hexalith.EventStore.Server.Events.EventEnvelope;

namespace Hexalith.EventStore.Server.Tests.Actors;

/// <summary>
/// Story 4.3 Task 6.1: Backpressure unit tests (AC: #1-#12).
/// </summary>
public class BackpressureTests {
    private const string DefaultActorId = "test-tenant:test-domain:agg-001";
    private const string PendingCommandCountKey = "pending_command_count";

    private static (AggregateActor Actor, IActorStateManager StateManager, IDomainServiceInvoker Invoker,
        IEventPublisher EventPublisher) CreateActor(
        string actorId = DefaultActorId,
        BackpressureOptions? backpressureOptions = null) {
        IActorStateManager stateManager = Substitute.For<IActorStateManager>();
        ILogger<AggregateActor> logger = Substitute.For<ILogger<AggregateActor>>();
        IDomainServiceInvoker invoker = Substitute.For<IDomainServiceInvoker>();
        ISnapshotManager snapshotManager = Substitute.For<ISnapshotManager>();
        ICommandStatusStore statusStore = Substitute.For<ICommandStatusStore>();
        IEventPublisher eventPublisher = Substitute.For<IEventPublisher>();
        IDeadLetterPublisher deadLetterPublisher = Substitute.For<IDeadLetterPublisher>();
        _ = deadLetterPublisher.PublishDeadLetterAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
            Arg.Any<DeadLetterMessage>(),
            Arg.Any<CancellationToken>())
            .Returns(true);
        var host = ActorHost.CreateForTest<AggregateActor>(
            new ActorTestOptions { ActorId = new ActorId(actorId) });
        var actor = new AggregateActor(
            host, logger, invoker, snapshotManager,
            new NoOpEventPayloadProtectionService(), statusStore, eventPublisher,
            Options.Create(new EventDrainOptions()),
            Options.Create(backpressureOptions ?? new BackpressureOptions()),
            deadLetterPublisher);

        PropertyInfo? prop = typeof(Actor).GetProperty("StateManager", BindingFlags.Public | BindingFlags.Instance);
        prop?.SetValue(actor, stateManager);

        // Default: no pipeline state (fresh command, not a resume)
        _ = stateManager.TryGetStateAsync<PipelineState>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<PipelineState>(false, default!));

        // Default: event publisher succeeds
        _ = eventPublisher.PublishEventsAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>())
            .Returns(callInfo => new EventPublishResult(true, callInfo.ArgAt<IReadOnlyList<EventEnvelope>>(1).Count, null));

        return (actor, stateManager, invoker, eventPublisher);
    }

    private static CommandEnvelope CreateCommand(
        string correlationId = "corr-1",
        string? causationId = "cause-1",
        string tenantId = "test-tenant",
        string domain = "test-domain",
        string aggregateId = "agg-001") => new(
        MessageId: "msg-1",
        CorrelationId: correlationId,
        CausationId: causationId,
        TenantId: tenantId,
        Domain: domain,
        AggregateId: aggregateId,
        CommandType: "CreateOrder",
        Payload: [1, 2, 3],
        UserId: "user-1",
        Extensions: null);

    private static void ConfigureNoDuplicate(IActorStateManager stateManager) {
        _ = stateManager.TryGetStateAsync<IdempotencyRecord>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<IdempotencyRecord>(false, default!));

        // No aggregate metadata (new aggregate)
        _ = stateManager.TryGetStateAsync<AggregateMetadata>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<AggregateMetadata>(false, default!));
    }

    private static void ConfigureDuplicate(IActorStateManager stateManager, string causationId, string correlationId) {
        var record = new IdempotencyRecord(causationId, correlationId, true, null, DateTimeOffset.UtcNow);
        _ = stateManager.TryGetStateAsync<IdempotencyRecord>($"idempotency:{causationId}", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<IdempotencyRecord>(true, record));

        _ = stateManager.TryGetStateAsync<IdempotencyRecord>(
            Arg.Is<string>(s => s != $"idempotency:{causationId}"), Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<IdempotencyRecord>(false, default!));
    }

    private static void SetupPendingCount(IActorStateManager stateManager, int count) {
        _ = stateManager.TryGetStateAsync<int>(PendingCommandCountKey, Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<int>(true, count));
    }

    private static void SetupNoPendingCount(IActorStateManager stateManager) {
        _ = stateManager.TryGetStateAsync<int>(PendingCommandCountKey, Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<int>(false, 0));
    }

    private static void SetupPendingCountSequence(IActorStateManager stateManager, params int[] counts) {
        // Set up a sequence of returns for successive calls
        var calls = counts.Select(c => new ConditionalValue<int>(c > 0, c)).ToArray();
        _ = stateManager.TryGetStateAsync<int>(PendingCommandCountKey, Arg.Any<CancellationToken>())
            .Returns(calls[0], calls.Skip(1).ToArray());
    }

    // --- AC #1: Backpressure rejects commands when pending count exceeds threshold ---

    [Fact]
    public async Task ProcessCommand_PendingCountAtThreshold_Rejected() {
        (AggregateActor actor, IActorStateManager stateManager, _, _) = CreateActor(
            backpressureOptions: new BackpressureOptions { MaxPendingCommandsPerAggregate = 5 });

        ConfigureNoDuplicate(stateManager);
        SetupPendingCount(stateManager, 5);

        CommandProcessingResult result = await actor.ProcessCommandAsync(CreateCommand());

        result.Accepted.ShouldBeFalse();
        result.BackpressureExceeded.ShouldBeTrue();
        result.ErrorMessage.ShouldNotBeNull();
        result.ErrorMessage.ShouldContain("Backpressure");
    }

    [Fact]
    public async Task ProcessCommand_PendingCountAboveThreshold_Rejected() {
        (AggregateActor actor, IActorStateManager stateManager, _, _) = CreateActor(
            backpressureOptions: new BackpressureOptions { MaxPendingCommandsPerAggregate = 5 });

        ConfigureNoDuplicate(stateManager);
        SetupPendingCount(stateManager, 10);

        CommandProcessingResult result = await actor.ProcessCommandAsync(CreateCommand());

        result.Accepted.ShouldBeFalse();
        result.BackpressureExceeded.ShouldBeTrue();
    }

    [Fact]
    public async Task ProcessCommand_PendingCountBelowThreshold_Accepted() {
        (AggregateActor actor, IActorStateManager stateManager, IDomainServiceInvoker invoker, _) = CreateActor(
            backpressureOptions: new BackpressureOptions { MaxPendingCommandsPerAggregate = 100 });

        ConfigureNoDuplicate(stateManager);
        // Sequence: first read returns 0 (backpressure check), second returns 1
        // for the final decrement after the no-op completes.
        SetupPendingCountSequence(stateManager, 0, 1);

        _ = invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>(), Arg.Any<CancellationToken>())
            .Returns(DomainResult.NoOp());

        CommandProcessingResult result = await actor.ProcessCommandAsync(CreateCommand());

        result.Accepted.ShouldBeTrue();
    }

    // --- AC #9: Idempotent commands bypass backpressure ---

    [Fact]
    public async Task ProcessCommand_DuplicateCommand_BypassesBackpressure() {
        (AggregateActor actor, IActorStateManager stateManager, _, _) = CreateActor(
            backpressureOptions: new BackpressureOptions { MaxPendingCommandsPerAggregate = 1 });

        ConfigureDuplicate(stateManager, "cause-1", "corr-1");

        // High pending count — but should be bypassed for idempotent commands
        SetupPendingCount(stateManager, 999);

        CommandProcessingResult result = await actor.ProcessCommandAsync(CreateCommand());

        result.Accepted.ShouldBeTrue();
    }

    // --- AC #3: Backpressure is per-aggregate, not system-wide ---

    [Fact]
    public async Task ProcessCommand_DifferentAggregates_IndependentBackpressure() {
        // Aggregate A has high pending count
        (AggregateActor actorA, IActorStateManager stateManagerA, _, _) = CreateActor(
            actorId: "test-tenant:test-domain:agg-A",
            backpressureOptions: new BackpressureOptions { MaxPendingCommandsPerAggregate = 5 });
        ConfigureNoDuplicate(stateManagerA);
        SetupPendingCount(stateManagerA, 10);

        // Aggregate B has zero pending count
        (AggregateActor actorB, IActorStateManager stateManagerB, IDomainServiceInvoker invokerB, _) = CreateActor(
            actorId: "test-tenant:test-domain:agg-B",
            backpressureOptions: new BackpressureOptions { MaxPendingCommandsPerAggregate = 5 });
        _ = stateManagerB.TryGetStateAsync<IdempotencyRecord>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<IdempotencyRecord>(false, default!));
        _ = stateManagerB.TryGetStateAsync<AggregateMetadata>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<AggregateMetadata>(false, default!));
        SetupPendingCountSequence(stateManagerB, 0, 0, 1);
        _ = invokerB.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>(), Arg.Any<CancellationToken>())
            .Returns(DomainResult.NoOp());

        CommandEnvelope commandA = CreateCommand(aggregateId: "agg-A");
        CommandEnvelope commandB = CreateCommand(correlationId: "corr-2", causationId: "cause-2", aggregateId: "agg-B");

        CommandProcessingResult resultA = await actorA.ProcessCommandAsync(commandA);
        CommandProcessingResult resultB = await actorB.ProcessCommandAsync(commandB);

        resultA.Accepted.ShouldBeFalse();
        resultA.BackpressureExceeded.ShouldBeTrue();
        resultB.Accepted.ShouldBeTrue();
    }

    // --- AC #4: Pending command counter tracks non-terminal pipeline states ---

    [Fact]
    public async Task ProcessCommand_Accepted_IncrementsPendingCount() {
        (AggregateActor actor, IActorStateManager stateManager, IDomainServiceInvoker invoker, _) = CreateActor();

        ConfigureNoDuplicate(stateManager);
        SetupPendingCountSequence(stateManager, 0, 1);
        _ = invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>(), Arg.Any<CancellationToken>())
            .Returns(DomainResult.NoOp());

        _ = await actor.ProcessCommandAsync(CreateCommand());

        // Verify counter was incremented (SetStateAsync called with PendingCommandCountKey = 1)
        await stateManager.Received().SetStateAsync(PendingCommandCountKey, 1, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessCommand_Completed_DecrementsPendingCount() {
        (AggregateActor actor, IActorStateManager stateManager, IDomainServiceInvoker invoker, _) = CreateActor();

        ConfigureNoDuplicate(stateManager);
        // Backpressure check reads 0 -> pass, final decrement reads 1 -> sets 0.
        SetupPendingCountSequence(stateManager, 0, 1);
        _ = invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>(), Arg.Any<CancellationToken>())
            .Returns(DomainResult.NoOp());

        _ = await actor.ProcessCommandAsync(CreateCommand());

        // Counter should be incremented then decremented (finally block)
        Received.InOrder(() => {
            stateManager.SetStateAsync(PendingCommandCountKey, 1, Arg.Any<CancellationToken>());
            stateManager.SetStateAsync(PendingCommandCountKey, 0, Arg.Any<CancellationToken>());
        });

        await stateManager.Received(3).SaveStateAsync(Arg.Any<CancellationToken>());
    }

    // --- AC #7: Counter initialization on first use ---

    [Fact]
    public async Task PendingCount_DefaultsToZero_WhenMissing() {
        (AggregateActor actor, IActorStateManager stateManager, IDomainServiceInvoker invoker, _) = CreateActor();

        ConfigureNoDuplicate(stateManager);
        // Missing state reads as 0, then the final decrement observes the staged value of 1.
        SetupPendingCountSequence(stateManager, 0, 1);
        _ = invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>(), Arg.Any<CancellationToken>())
            .Returns(DomainResult.NoOp());

        // Should succeed (treats missing count as 0)
        CommandProcessingResult result = await actor.ProcessCommandAsync(CreateCommand());

        result.Accepted.ShouldBeTrue();
    }

    // --- AC #5: Counter survives actor deactivation and restart ---

    [Fact]
    public async Task PendingCount_SurvivesActorReactivation() {
        (AggregateActor actor, IActorStateManager stateManager, _, _) = CreateActor(
            backpressureOptions: new BackpressureOptions { MaxPendingCommandsPerAggregate = 5 });

        ConfigureNoDuplicate(stateManager);
        // Simulate persisted count from previous activation
        SetupPendingCount(stateManager, 5);

        CommandProcessingResult result = await actor.ProcessCommandAsync(CreateCommand());

        result.Accepted.ShouldBeFalse();
        result.BackpressureExceeded.ShouldBeTrue();
    }

    // --- AC #1 extension: Fast-reject (no state rehydration, no domain invocation) ---

    [Fact]
    public async Task ProcessCommand_BackpressureRejected_NoStateRehydrationOrDomainInvocation() {
        (AggregateActor actor, IActorStateManager stateManager, IDomainServiceInvoker invoker, _) = CreateActor(
            backpressureOptions: new BackpressureOptions { MaxPendingCommandsPerAggregate = 5 });

        ConfigureNoDuplicate(stateManager);
        SetupPendingCount(stateManager, 10);

        _ = await actor.ProcessCommandAsync(CreateCommand());

        // Domain service should NOT be called on backpressure rejection
        await invoker.DidNotReceive().InvokeAsync(
            Arg.Any<CommandEnvelope>(), Arg.Any<object?>(), Arg.Any<CancellationToken>());
    }

    // --- Fail-open policy: State read failure allows command through ---

    [Fact]
    public async Task ProcessCommand_BackpressureCheckStateReadFails_FailsOpen() {
        (AggregateActor actor, IActorStateManager stateManager, IDomainServiceInvoker invoker, _) = CreateActor(
            backpressureOptions: new BackpressureOptions { MaxPendingCommandsPerAggregate = 5 });

        ConfigureNoDuplicate(stateManager);
        _ = invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>(), Arg.Any<CancellationToken>())
            .Returns(DomainResult.NoOp());

        // First call (backpressure check read) throws, subsequent calls succeed
        int callCount = 0;
        _ = stateManager.TryGetStateAsync<int>(PendingCommandCountKey, Arg.Any<CancellationToken>())
            .Returns(_ => {
                int call = Interlocked.Increment(ref callCount);
                if (call == 1) throw new InvalidOperationException("State store unavailable");
                if (call == 2) return new ConditionalValue<int>(false, 0);   // increment read
                return new ConditionalValue<int>(true, 1);                    // decrement read
            });

        CommandProcessingResult result = await actor.ProcessCommandAsync(CreateCommand());

        result.Accepted.ShouldBeTrue();
    }

    // --- AC #6: Drain success decrements counter ---

    [Fact]
    public async Task DrainSuccess_DecrementsPendingCount() {
        (AggregateActor actor, IActorStateManager stateManager, _, IEventPublisher eventPublisher) = CreateActor();

        var record = new UnpublishedEventsRecord(
            "corr-drain", StartSequence: 1, EndSequence: 2, EventCount: 2,
            CommandType: "CreateOrder", IsRejection: false,
            FailedAt: DateTimeOffset.UtcNow, RetryCount: 0, LastFailureReason: "Pub/sub unavailable");

        _ = stateManager.TryGetStateAsync<UnpublishedEventsRecord>(
            "drain:corr-drain", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<UnpublishedEventsRecord>(true, record));

        // Configure events in state
        for (int seq = 1; seq <= 2; seq++) {
            var evt = new EventEnvelope(
                "msg-1", "agg-001", "test-aggregate", "test-tenant", "test-domain", seq, 0, DateTimeOffset.UtcNow,
                "corr-drain", $"cause-{seq}", "user-1", "1.0.0", "OrderCreated", 1, "json",
                [1, 2, 3], null);
            _ = stateManager.TryGetStateAsync<EventEnvelope>(
                $"test-tenant:test-domain:agg-001:events:{seq}", Arg.Any<CancellationToken>())
                .Returns(new ConditionalValue<EventEnvelope>(true, evt));
        }

        // Pending count: 1 drain pending
        _ = stateManager.TryGetStateAsync<int>(PendingCommandCountKey, Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<int>(true, 1));

        _ = eventPublisher.PublishEventsAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>())
            .Returns(new EventPublishResult(true, 2, null));

        await actor.ReceiveReminderAsync("drain-unpublished-corr-drain", [], TimeSpan.Zero, TimeSpan.Zero);

        // Verify counter was decremented (1 -> 0)
        await stateManager.Received().SetStateAsync(PendingCommandCountKey, 0, Arg.Any<CancellationToken>());
    }

    // --- Tenant mismatch decrements counter via try/finally ---

    [Fact]
    public async Task ProcessCommand_TenantMismatch_DoesNotTouchPendingCount() {
        // Create actor with actorId that has different tenant than command
        (AggregateActor actor, IActorStateManager stateManager, _, _) = CreateActor(
            actorId: "different-tenant:test-domain:agg-001");

        // Set up idempotency (no duplicate)
        _ = stateManager.TryGetStateAsync<IdempotencyRecord>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<IdempotencyRecord>(false, default!));
        _ = stateManager.TryGetStateAsync<AggregateMetadata>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<AggregateMetadata>(false, default!));

        // The command has tenantId "test-tenant" but actor is "different-tenant" -> TenantMismatchException
        CommandProcessingResult result = await actor.ProcessCommandAsync(CreateCommand());

        // Should be rejected before any backpressure state access.
        result.Accepted.ShouldBeFalse();

        await stateManager.DidNotReceive().TryGetStateAsync<int>(PendingCommandCountKey, Arg.Any<CancellationToken>());
        await stateManager.DidNotReceive().SetStateAsync(PendingCommandCountKey, Arg.Any<int>(), Arg.Any<CancellationToken>());
    }
}
