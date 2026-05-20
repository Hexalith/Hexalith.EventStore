using System.Diagnostics;

using Dapr.Actors;
using Dapr.Actors.Runtime;

using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.Telemetry;
using Hexalith.EventStore.Server.Tests.TestUtilities;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

using EventEnvelope = Hexalith.EventStore.Server.Events.EventEnvelope;

namespace Hexalith.EventStore.Server.Tests.Actors;

// ATDD red-phase scaffolds for story:
//   _bmad-output/implementation-artifacts/post-epic-deferred-dw1-projection-and-drain-hardening.md
// AC #7 — Drain poison/terminal disposition with side-effect invariants (no publish, no record
//          removal, no pending-counter decrement, no reminder unregister).
// AC #8 — Drain activity failure reason MUST use stable bounded reason codes (not raw exception
//          text or arbitrary free-form message). Vocabulary: drain_event_count_mismatch,
//          drain_missing_event, drain_publish_failed, drain_terminal_failure, unknown.
// AC #9 — Reminder re-entrancy idempotence: repeated reminder fire must not produce duplicate
//          publish, duplicate counter decrement, or duplicate reminder unregister.
public class Dw1DrainHardeningAtddTests {
    private const string SkipReasonAc7 = "ATDD red phase — DW1 AC#7 (drain poison disposition). Remove Skip when implementing.";
    private const string SkipReasonAc8 = "ATDD red phase — DW1 AC#8 (drain stable reason codes). Remove Skip when implementing.";
    private const string SkipReasonAc9 = "ATDD red phase — DW1 AC#9 (reminder re-entrancy idempotence). Remove Skip when implementing.";

    // ---------- AC #8: Stable activity reason codes ----------

    [Fact(Skip = SkipReasonAc8)]
    public async Task DrainEventCountMismatch_ActivityFailureReasonTagIsStableReasonCode() {
        // Today the drain code sets activity tag eventstore.failure_reason to the raw exception
        // message (e.g. "Drain record EventCount mismatch for ..."). After DW1 it must be the
        // stable bounded code drain_event_count_mismatch.
        Activity? captured = await CaptureDrainActivityAsync(setupRecord: stateManager => {
            var record = new UnpublishedEventsRecord(
                CorrelationId: "corr-drain",
                StartSequence: 10,
                EndSequence: 12,
                EventCount: 4, // mismatch — range is 3 events but record says 4
                CommandType: "CreateOrder",
                IsRejection: false,
                FailedAt: DateTimeOffset.UtcNow,
                RetryCount: 0,
                LastFailureReason: null);
            _ = stateManager.TryGetStateAsync<UnpublishedEventsRecord>(
                "drain:corr-drain", Arg.Any<CancellationToken>())
                .Returns(new ConditionalValue<UnpublishedEventsRecord>(true, record));
        });

        _ = captured.ShouldNotBeNull();
        captured.GetTagItem("eventstore.failure_reason").ShouldBe("drain_event_count_mismatch");
    }

    [Fact(Skip = SkipReasonAc8)]
    public async Task DrainMissingEventInRange_ActivityFailureReasonTagIsStableReasonCode() {
        Activity? captured = await CaptureDrainActivityAsync(setupRecord: stateManager => {
            var record = new UnpublishedEventsRecord(
                CorrelationId: "corr-drain",
                StartSequence: 10,
                EndSequence: 12,
                EventCount: 3,
                CommandType: "CreateOrder",
                IsRejection: false,
                FailedAt: DateTimeOffset.UtcNow,
                RetryCount: 0,
                LastFailureReason: null);
            _ = stateManager.TryGetStateAsync<UnpublishedEventsRecord>(
                "drain:corr-drain", Arg.Any<CancellationToken>())
                .Returns(new ConditionalValue<UnpublishedEventsRecord>(true, record));

            // Configure events 10 and 12 only — sequence 11 missing.
            var metadata = new AggregateMetadata(12, DateTimeOffset.UtcNow, null);
            _ = stateManager.TryGetStateAsync<AggregateMetadata>(
                "test-tenant:test-domain:agg-001:metadata", Arg.Any<CancellationToken>())
                .Returns(new ConditionalValue<AggregateMetadata>(true, metadata));
            foreach (long seq in new long[] { 10, 12 }) {
                var evt = new EventEnvelope(
                    "msg-1", "agg-001", "test-aggregate", "test-tenant", "test-domain", seq, 0, DateTimeOffset.UtcNow,
                    "corr-drain", $"cause-{seq}", "user-1", "1.0.0", "OrderCreated", 1, "json", [1, 2, 3], null);
                _ = stateManager.TryGetStateAsync<EventEnvelope>(
                    $"test-tenant:test-domain:agg-001:events:{seq}", Arg.Any<CancellationToken>())
                    .Returns(new ConditionalValue<EventEnvelope>(true, evt));
            }
        });

        _ = captured.ShouldNotBeNull();
        captured.GetTagItem("eventstore.failure_reason").ShouldBe("drain_missing_event");
    }

    [Fact(Skip = SkipReasonAc8)]
    public async Task DrainPublishFailed_ActivityFailureReasonTagIsStableReasonCode() {
        // Pub/sub publish returns failure — current code stores publishResult.FailureReason
        // (free-form publisher text) on the activity tag. DW1 requires the stable code
        // drain_publish_failed; the original publisher text remains in structured logs.
        Activity? captured = await CaptureDrainActivityAsync(
            setupRecord: stateManager => {
                var record = new UnpublishedEventsRecord(
                    CorrelationId: "corr-drain",
                    StartSequence: 1,
                    EndSequence: 2,
                    EventCount: 2,
                    CommandType: "CreateOrder",
                    IsRejection: false,
                    FailedAt: DateTimeOffset.UtcNow,
                    RetryCount: 0,
                    LastFailureReason: null);
                _ = stateManager.TryGetStateAsync<UnpublishedEventsRecord>(
                    "drain:corr-drain", Arg.Any<CancellationToken>())
                    .Returns(new ConditionalValue<UnpublishedEventsRecord>(true, record));
                ConfigureContiguousEvents(stateManager, startSequence: 1, eventCount: 2);
            },
            setupPublisher: publisher => _ = publisher.PublishEventsAsync(
                    Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
                    Arg.Any<IReadOnlyList<EventEnvelope>>(),
                    Arg.Any<string>(),
                    Arg.Any<CancellationToken>())
                    .Returns(new EventPublishResult(false, 0, "pubsub component unavailable")));

        _ = captured.ShouldNotBeNull();
        captured.GetTagItem("eventstore.failure_reason").ShouldBe("drain_publish_failed");
    }

    [Fact(Skip = SkipReasonAc8)]
    public async Task DrainAnyClassifiedFailure_ActivityFailureReasonTagDoesNotContainRawExceptionMessage() {
        // Privacy/cardinality invariant: even if the inner failure is "Object reference not set
        // to an instance of an object." the activity tag must not surface that text — only
        // the bounded reason code goes on the tag.
        Activity? captured = await CaptureDrainActivityAsync(setupRecord: stateManager => {
            var record = new UnpublishedEventsRecord(
                CorrelationId: "corr-drain",
                StartSequence: 1,
                EndSequence: 1,
                EventCount: 99, // intentional mismatch to trigger integrity failure
                CommandType: "CreateOrder",
                IsRejection: false,
                FailedAt: DateTimeOffset.UtcNow,
                RetryCount: 0,
                LastFailureReason: null);
            _ = stateManager.TryGetStateAsync<UnpublishedEventsRecord>(
                "drain:corr-drain", Arg.Any<CancellationToken>())
                .Returns(new ConditionalValue<UnpublishedEventsRecord>(true, record));
        });

        _ = captured.ShouldNotBeNull();
        string? reason = captured.GetTagItem("eventstore.failure_reason") as string;
        _ = reason.ShouldNotBeNull();
        reason.ShouldNotContain("Object reference");
        reason.ShouldNotContain("Drain record EventCount mismatch"); // raw exception text
        reason.ShouldNotContain("System.");                          // raw exception class names
        reason.Length.ShouldBeLessThan(64); // bounded — not a freeform sentence
    }

    // ---------- AC #7: Drain poison side-effect invariants ----------

    [Fact(Skip = SkipReasonAc7)]
    public async Task DrainEventCountMismatch_DoesNotDecrementPendingCommandCount() {
        // Existing tests already prove no publish / no remove / no reminder unregister.
        // This scaffold pins the additional invariant: the pending_command_count must NOT
        // be decremented on integrity failure (R4-A6).
        (AggregateActor actor, IActorStateManager stateManager, _, IEventPublisher publisher, _) = CreateActor();
        var record = new UnpublishedEventsRecord(
            CorrelationId: "corr-drain",
            StartSequence: 10,
            EndSequence: 12,
            EventCount: 4,
            CommandType: "CreateOrder",
            IsRejection: false,
            FailedAt: DateTimeOffset.UtcNow,
            RetryCount: 0,
            LastFailureReason: null);
        _ = stateManager.TryGetStateAsync<UnpublishedEventsRecord>(
            "drain:corr-drain", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<UnpublishedEventsRecord>(true, record));
        _ = stateManager.TryGetStateAsync<int>("pending_command_count", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<int>(true, 5));

        await actor.ReceiveReminderAsync("drain-unpublished-corr-drain", [], TimeSpan.Zero, TimeSpan.Zero);

        // Counter must not be set to 4 (would mean it was decremented on integrity failure).
        await stateManager.DidNotReceive().SetStateAsync(
            "pending_command_count",
            Arg.Is<int>(v => v < 5),
            Arg.Any<CancellationToken>());
        // And no publish happened.
        _ = await publisher.DidNotReceive().PublishEventsAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    // ---------- AC #9: Reminder re-entrancy idempotence ----------

    [Fact(Skip = SkipReasonAc9)]
    public async Task DrainReminder_FiredTwiceForSameCorrelationId_PublishesAtMostOnce() {
        // Dapr actor reminders respect turn-based concurrency, but DW1 requires explicit
        // proof that two sequential reminder firings of the same correlation id do not
        // produce duplicate publish events. After a successful drain, the record is
        // removed; the second reminder fire must hit the orphaned-reminder branch and
        // NOT call PublishEventsAsync again.
        (AggregateActor actor, IActorStateManager stateManager, _, IEventPublisher publisher, _, ActorTimerManager timerManager) = CreateActorWithTimerManager();

        var record = new UnpublishedEventsRecord(
            CorrelationId: "corr-drain",
            StartSequence: 1,
            EndSequence: 2,
            EventCount: 2,
            CommandType: "CreateOrder",
            IsRejection: false,
            FailedAt: DateTimeOffset.UtcNow,
            RetryCount: 0,
            LastFailureReason: null);

        _ = stateManager.TryGetStateAsync<UnpublishedEventsRecord>(
            "drain:corr-drain", Arg.Any<CancellationToken>())
            .Returns(
                _ => new ConditionalValue<UnpublishedEventsRecord>(true, record),
                _ => new ConditionalValue<UnpublishedEventsRecord>(false, default!));
        ConfigureContiguousEvents(stateManager, startSequence: 1, eventCount: 2);
        _ = publisher.PublishEventsAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>())
            .Returns(new EventPublishResult(true, 2, null));

        await actor.ReceiveReminderAsync("drain-unpublished-corr-drain", [], TimeSpan.Zero, TimeSpan.Zero);
        await actor.ReceiveReminderAsync("drain-unpublished-corr-drain", [], TimeSpan.Zero, TimeSpan.Zero);

        _ = await publisher.Received(1).PublishEventsAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            "corr-drain",
            Arg.Any<CancellationToken>());
    }

    [Fact(Skip = SkipReasonAc9)]
    public async Task DrainReminder_FiredTwiceForSameCorrelationId_DecrementsPendingCounterAtMostOnce() {
        (AggregateActor actor, IActorStateManager stateManager, _, IEventPublisher publisher, _, _) = CreateActorWithTimerManager();
        var record = new UnpublishedEventsRecord(
            CorrelationId: "corr-drain",
            StartSequence: 1,
            EndSequence: 2,
            EventCount: 2,
            CommandType: "CreateOrder",
            IsRejection: false,
            FailedAt: DateTimeOffset.UtcNow,
            RetryCount: 0,
            LastFailureReason: null);

        _ = stateManager.TryGetStateAsync<UnpublishedEventsRecord>(
            "drain:corr-drain", Arg.Any<CancellationToken>())
            .Returns(
                _ => new ConditionalValue<UnpublishedEventsRecord>(true, record),
                _ => new ConditionalValue<UnpublishedEventsRecord>(false, default!));
        ConfigureContiguousEvents(stateManager, startSequence: 1, eventCount: 2);
        _ = stateManager.TryGetStateAsync<int>("pending_command_count", Arg.Any<CancellationToken>())
            .Returns(
                _ => new ConditionalValue<int>(true, 3),
                _ => new ConditionalValue<int>(true, 2));
        _ = publisher.PublishEventsAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>())
            .Returns(new EventPublishResult(true, 2, null));

        await actor.ReceiveReminderAsync("drain-unpublished-corr-drain", [], TimeSpan.Zero, TimeSpan.Zero);
        await actor.ReceiveReminderAsync("drain-unpublished-corr-drain", [], TimeSpan.Zero, TimeSpan.Zero);

        // Only one decrement (3 -> 2). Second reminder finds no record and must not decrement.
        await stateManager.Received(1).SetStateAsync(
            "pending_command_count",
            Arg.Is<int>(v => v == 2),
            Arg.Any<CancellationToken>());
        await stateManager.DidNotReceive().SetStateAsync(
            "pending_command_count",
            Arg.Is<int>(v => v < 2),
            Arg.Any<CancellationToken>());
    }

    [Fact(Skip = SkipReasonAc9)]
    public async Task DrainReminder_FiredTwiceForSameCorrelationId_UnregistersReminderAtMostOnce() {
        (AggregateActor actor, IActorStateManager stateManager, _, IEventPublisher publisher, _, ActorTimerManager timerManager) = CreateActorWithTimerManager();
        var record = new UnpublishedEventsRecord(
            CorrelationId: "corr-drain",
            StartSequence: 1,
            EndSequence: 2,
            EventCount: 2,
            CommandType: "CreateOrder",
            IsRejection: false,
            FailedAt: DateTimeOffset.UtcNow,
            RetryCount: 0,
            LastFailureReason: null);

        _ = stateManager.TryGetStateAsync<UnpublishedEventsRecord>(
            "drain:corr-drain", Arg.Any<CancellationToken>())
            .Returns(
                _ => new ConditionalValue<UnpublishedEventsRecord>(true, record),
                _ => new ConditionalValue<UnpublishedEventsRecord>(false, default!));
        ConfigureContiguousEvents(stateManager, startSequence: 1, eventCount: 2);
        _ = publisher.PublishEventsAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>())
            .Returns(new EventPublishResult(true, 2, null));

        await actor.ReceiveReminderAsync("drain-unpublished-corr-drain", [], TimeSpan.Zero, TimeSpan.Zero);
        await actor.ReceiveReminderAsync("drain-unpublished-corr-drain", [], TimeSpan.Zero, TimeSpan.Zero);

        // Both invocations attempt unregister (success path + orphan path). DW1 must prove
        // this is idempotent — actor reminders are turn-based, but the timer manager mock
        // should see at most one effective unregister with the drain reminder name.
        await timerManager.Received(1).UnregisterReminderAsync(
            Arg.Is<ActorReminderToken>(t => t.Name == "drain-unpublished-corr-drain"));
    }

    // ---------- Helpers ----------

    private static async Task<Activity?> CaptureDrainActivityAsync(
        Action<IActorStateManager> setupRecord,
        Action<IEventPublisher>? setupPublisher = null) {
        Activity? captured = null;
        using var listener = new ActivityListener {
            ShouldListenTo = source => source.Name == EventStoreActivitySource.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activity => {
                if (activity.OperationName == EventStoreActivitySource.EventsDrain) {
                    captured = activity;
                }
            },
        };
        ActivitySource.AddActivityListener(listener);

        (AggregateActor actor, IActorStateManager stateManager, _, IEventPublisher publisher, _) = CreateActor();
        setupRecord(stateManager);
        setupPublisher?.Invoke(publisher);

        await actor.ReceiveReminderAsync("drain-unpublished-corr-drain", [], TimeSpan.Zero, TimeSpan.Zero);

        return captured;
    }

    private static (AggregateActor Actor, IActorStateManager StateManager, ILogger<AggregateActor> Logger,
        IEventPublisher EventPublisher, ICommandStatusStore StatusStore) CreateActor(
        string actorId = "test-tenant:test-domain:agg-001") {
        IActorStateManager stateManager = Substitute.For<IActorStateManager>();
        ILogger<AggregateActor> logger = Substitute.For<ILogger<AggregateActor>>();
        IDomainServiceInvoker invoker = Substitute.For<IDomainServiceInvoker>();
        ISnapshotManager snapshotManager = Substitute.For<ISnapshotManager>();
        ICommandStatusStore statusStore = Substitute.For<ICommandStatusStore>();
        IEventPublisher eventPublisher = Substitute.For<IEventPublisher>();
        var host = ActorHost.CreateForTest<AggregateActor>(
            new ActorTestOptions { ActorId = new ActorId(actorId) });
        var actor = new AggregateActor(
            host, logger, invoker, snapshotManager, new NoOpEventPayloadProtectionService(),
            statusStore, eventPublisher,
            Options.Create(new EventDrainOptions()),
            Options.Create(new BackpressureOptions()),
            Substitute.For<IDeadLetterPublisher>());
        ActorStateManagerTestHelper.SetStateManager(actor, stateManager);
        return (actor, stateManager, logger, eventPublisher, statusStore);
    }

    private static (AggregateActor Actor, IActorStateManager StateManager, ILogger<AggregateActor> Logger,
        IEventPublisher EventPublisher, ICommandStatusStore StatusStore, ActorTimerManager TimerManager) CreateActorWithTimerManager(
        string actorId = "test-tenant:test-domain:agg-001") {
        IActorStateManager stateManager = Substitute.For<IActorStateManager>();
        ILogger<AggregateActor> logger = Substitute.For<ILogger<AggregateActor>>();
        IDomainServiceInvoker invoker = Substitute.For<IDomainServiceInvoker>();
        ISnapshotManager snapshotManager = Substitute.For<ISnapshotManager>();
        ICommandStatusStore statusStore = Substitute.For<ICommandStatusStore>();
        IEventPublisher eventPublisher = Substitute.For<IEventPublisher>();
        ActorTimerManager timerManager = Substitute.For<ActorTimerManager>();
        var host = ActorHost.CreateForTest<AggregateActor>(
            new ActorTestOptions { ActorId = new ActorId(actorId), TimerManager = timerManager });
        var actor = new AggregateActor(
            host, logger, invoker, snapshotManager, new NoOpEventPayloadProtectionService(),
            statusStore, eventPublisher,
            Options.Create(new EventDrainOptions()),
            Options.Create(new BackpressureOptions()),
            Substitute.For<IDeadLetterPublisher>());
        ActorStateManagerTestHelper.SetStateManager(actor, stateManager);
        return (actor, stateManager, logger, eventPublisher, statusStore, timerManager);
    }

    private static void ConfigureContiguousEvents(IActorStateManager stateManager, long startSequence, int eventCount) {
        long endSequence = startSequence + eventCount - 1;
        var metadata = new AggregateMetadata(endSequence, DateTimeOffset.UtcNow, null);
        _ = stateManager.TryGetStateAsync<AggregateMetadata>(
            "test-tenant:test-domain:agg-001:metadata", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<AggregateMetadata>(true, metadata));
        for (long seq = startSequence; seq <= endSequence; seq++) {
            var evt = new EventEnvelope(
                "msg-1", "agg-001", "test-aggregate", "test-tenant", "test-domain", seq, 0, DateTimeOffset.UtcNow,
                "corr-drain", $"cause-{seq}", "user-1", "1.0.0", "OrderCreated", 1, "json", [1, 2, 3], null);
            _ = stateManager.TryGetStateAsync<EventEnvelope>(
                $"test-tenant:test-domain:agg-001:events:{seq}", Arg.Any<CancellationToken>())
                .Returns(new ConditionalValue<EventEnvelope>(true, evt));
        }
    }
}
