
using System.Diagnostics;

using Dapr.Actors;
using Dapr.Actors.Runtime;

using Hexalith.EventStore.Contracts.Commands;
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
/// <summary>
/// Story 4.2: Drain recovery and end-to-end drain cycle tests.
/// Verifies ReceiveReminderAsync, DrainUnpublishedEventsAsync, and full drain lifecycle
/// (AC: #1, #4, #5, #6, #9, #10, #12).
/// </summary>
public class EventDrainRecoveryTests {
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
        var actor = new AggregateActor(host, logger, invoker, snapshotManager, new NoOpEventPayloadProtectionService(), statusStore, eventPublisher, Options.Create(new EventDrainOptions()), Options.Create(new BackpressureOptions()), Substitute.For<IDeadLetterPublisher>());

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
        var actor = new AggregateActor(host, logger, invoker, snapshotManager, new NoOpEventPayloadProtectionService(), statusStore, eventPublisher, Options.Create(new EventDrainOptions()), Options.Create(new BackpressureOptions()), Substitute.For<IDeadLetterPublisher>());

        ActorStateManagerTestHelper.SetStateManager(actor, stateManager);

        return (actor, stateManager, logger, eventPublisher, statusStore, timerManager);
    }

    private static UnpublishedEventsRecord CreateDrainRecord(
        string correlationId = "corr-drain",
        int eventCount = 2,
        int retryCount = 0,
        bool isRejection = false,
        string? messageId = null) => new(
        correlationId,
        StartSequence: 1,
        EndSequence: eventCount,
        EventCount: eventCount,
        CommandType: "CreateOrder",
        IsRejection: isRejection,
        FailedAt: DateTimeOffset.UtcNow,
        RetryCount: retryCount,
        LastFailureReason: "Pub/sub unavailable",
        MessageId: messageId);

    /// <summary>
    /// Seeds the persisted event range. Story 4.4: each seeded event carries a DISTINCT message id
    /// derived from its sequence, so an identity assertion of the form
    /// <c>ShouldAllBe(id =&gt; id == "msg-1")</c> can no longer be satisfied by the fixture itself.
    /// </summary>
    private static void ConfigureEventsInState(
        IActorStateManager stateManager,
        int eventCount,
        string correlationId = "corr-drain",
        int startSequence = 1) {
        int endSequence = startSequence + eventCount - 1;
        var metadata = new AggregateMetadata(endSequence, DateTimeOffset.UtcNow, null);
        _ = stateManager.TryGetStateAsync<AggregateMetadata>(
            "test-tenant:test-domain:agg-001:metadata", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<AggregateMetadata>(true, metadata));

        for (int seq = startSequence; seq <= endSequence; seq++) {
            var evt = new EventEnvelope(
                $"evt-msg-{seq}", "agg-001", "test-aggregate", "test-tenant", "test-domain", seq, 0, DateTimeOffset.UtcNow,
                correlationId, $"cause-{seq}", "user-1", "1.0.0", "OrderCreated", 1, "json",
                [1, 2, 3], null);
            _ = stateManager.TryGetStateAsync<EventEnvelope>(
                $"test-tenant:test-domain:agg-001:events:{seq}", Arg.Any<CancellationToken>())
                .Returns(new ConditionalValue<EventEnvelope>(true, evt));
        }
    }

    private static void ConfigureThreeEventDrainWithMissingSequence(
        IActorStateManager stateManager,
        int missingSequence,
        string correlationId = "corr-drain",
        int startSequence = 10) {
        const int eventCount = 3;
        int endSequence = startSequence + eventCount - 1;
        for (int seq = startSequence; seq <= endSequence; seq++) {
            if (seq == missingSequence) {
                continue;
            }

            var evt = new EventEnvelope(
                "msg-1", "agg-001", "test-aggregate", "test-tenant", "test-domain", seq, 0, DateTimeOffset.UtcNow,
                correlationId, $"cause-{seq}", "user-1", "1.0.0", "OrderCreated", 1, "json",
                [1, 2, 3], null);
            _ = stateManager.TryGetStateAsync<EventEnvelope>(
                $"test-tenant:test-domain:agg-001:events:{seq}", Arg.Any<CancellationToken>())
                .Returns(new ConditionalValue<EventEnvelope>(true, evt));
        }
    }

    private static async Task AssertDrainIntegrityFailureAsync(
        IActorStateManager stateManager,
        IEventPublisher eventPublisher,
        ActorTimerManager timerManager,
        string expectedRedactedFailureMustNotContain,
        int expectedRetryCount = 1,
        string correlationId = "corr-drain") {
        _ = await eventPublisher.DidNotReceive().PublishEventsAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<bool>());

        await stateManager.DidNotReceive().RemoveStateAsync(
            $"drain:{correlationId}", Arg.Any<CancellationToken>());

        await timerManager.DidNotReceive().UnregisterReminderAsync(
            Arg.Any<ActorReminderToken>());

        await stateManager.Received(1).SetStateAsync(
            $"drain:{correlationId}",
            Arg.Is<UnpublishedEventsRecord>(r =>
                r.RetryCount == expectedRetryCount
                && r.LastFailureReason != null
                && r.LastFailureReason.Contains("Protected data diagnostic details were redacted.")
                && r.LastFailureReason.Contains("ReasonCode=protected-data-diagnostic-redacted")
                && r.LastFailureReason.Contains("Stage=drain")
                && !r.LastFailureReason.Contains(expectedRedactedFailureMustNotContain)),
            Arg.Any<CancellationToken>());

        await stateManager.Received().SaveStateAsync(Arg.Any<CancellationToken>());

        await stateManager.DidNotReceive().SetStateAsync(
            "pending_command_count",
            Arg.Any<int>(),
            Arg.Any<CancellationToken>());
    }

    private static Task<Activity> CaptureDrainActivityAsync(Func<Task> action)
        => CaptureDrainActivityAsync(correlationId: "corr-drain", action);

    private static async Task<Activity> CaptureDrainActivityAsync(string correlationId, Func<Task> action) {
        var stopped = new List<Activity>();
        using var listener = new ActivityListener {
            ShouldListenTo = source => source.Name == EventStoreActivitySource.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => {
                // ActivitySource.AddActivityListener registers the listener process-globally; xUnit collections
                // run in parallel by default, so filter strictly by the operation name AND the correlationId
                // tag set by AggregateActor before action() runs. This prevents cross-test capture between
                // EventDrainRecoveryTests and Dw8DrainReasonClassifierTests when both fire drain activities.
                if (activity.OperationName == EventStoreActivitySource.EventsDrain
                    && string.Equals(
                        activity.GetTagItem(EventStoreActivitySource.TagCorrelationId)?.ToString(),
                        correlationId,
                        StringComparison.Ordinal)) {
                    stopped.Add(activity);
                }
            },
        };
        ActivitySource.AddActivityListener(listener);

        await action().ConfigureAwait(false);

        stopped.Count.ShouldBe(1);
        return stopped[0];
    }

    // --- Task 7.2: Drain succeeds, events re-published ---

    [Fact]
    public async Task ReceiveReminder_DrainSucceeds_EventsRePublished() {
        // Arrange
        (AggregateActor actor, IActorStateManager stateManager, _, IEventPublisher eventPublisher, _) = CreateActor();
        UnpublishedEventsRecord record = CreateDrainRecord();

        _ = stateManager.TryGetStateAsync<UnpublishedEventsRecord>(
            "drain:corr-drain", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<UnpublishedEventsRecord>(true, record));

        ConfigureEventsInState(stateManager, 2);

        _ = eventPublisher.PublishEventsAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<bool>())
            .Returns(new EventPublishResult(true, 2, null));

        // Act
        await actor.ReceiveReminderAsync("drain-unpublished-corr-drain", [], TimeSpan.Zero, TimeSpan.Zero);

        // Assert
        _ = await eventPublisher.Received(1).PublishEventsAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
            Arg.Is<IReadOnlyList<EventEnvelope>>(e => e.Count == 2),
            "corr-drain",
            Arg.Any<CancellationToken>(),
            Arg.Any<bool>());
    }

    // --- Task 7.3: Drain succeeds, reminder unregistered ---

    [Fact]
    public async Task ReceiveReminder_DrainSucceeds_RecordRemoved() {
        // Arrange
        (AggregateActor actor, IActorStateManager stateManager, _, IEventPublisher eventPublisher, _) = CreateActor();
        UnpublishedEventsRecord record = CreateDrainRecord();

        _ = stateManager.TryGetStateAsync<UnpublishedEventsRecord>(
            "drain:corr-drain", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<UnpublishedEventsRecord>(true, record));

        ConfigureEventsInState(stateManager, 2);

        _ = eventPublisher.PublishEventsAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<bool>())
            .Returns(new EventPublishResult(true, 2, null));

        // Act
        await actor.ReceiveReminderAsync("drain-unpublished-corr-drain", [], TimeSpan.Zero, TimeSpan.Zero);

        // Assert -- record removed from state
        await stateManager.Received(1).RemoveStateAsync(
            "drain:corr-drain", Arg.Any<CancellationToken>());

        // SaveStateAsync called to commit removal
        await stateManager.Received().SaveStateAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReceiveReminder_DrainSucceeds_ReminderUnregistered() {
        // Arrange
        (AggregateActor actor, IActorStateManager stateManager, _, IEventPublisher eventPublisher, _, ActorTimerManager timerManager) = CreateActorWithTimerManager();
        UnpublishedEventsRecord record = CreateDrainRecord();

        _ = stateManager.TryGetStateAsync<UnpublishedEventsRecord>(
            "drain:corr-drain", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<UnpublishedEventsRecord>(true, record));

        ConfigureEventsInState(stateManager, 2);

        _ = eventPublisher.PublishEventsAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<bool>())
            .Returns(new EventPublishResult(true, 2, null));

        // Act
        await actor.ReceiveReminderAsync("drain-unpublished-corr-drain", [], TimeSpan.Zero, TimeSpan.Zero);

        // Assert
        await timerManager.Received(1).UnregisterReminderAsync(
            Arg.Any<ActorReminderToken>());
    }

    // --- Task 7.4: Drain succeeds, advisory status updated ---

    [Fact]
    public async Task ReceiveReminder_DrainSucceeds_AdvisoryStatusUpdatedToCompleted() {
        // Arrange
        (AggregateActor actor, IActorStateManager stateManager, _, IEventPublisher eventPublisher, ICommandStatusStore statusStore) = CreateActor();
        UnpublishedEventsRecord record = CreateDrainRecord(isRejection: false);

        _ = stateManager.TryGetStateAsync<UnpublishedEventsRecord>(
            "drain:corr-drain", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<UnpublishedEventsRecord>(true, record));

        ConfigureEventsInState(stateManager, 2);

        _ = eventPublisher.PublishEventsAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<bool>())
            .Returns(new EventPublishResult(true, 2, null));

        // Act
        await actor.ReceiveReminderAsync("drain-unpublished-corr-drain", [], TimeSpan.Zero, TimeSpan.Zero);

        // Assert
        await statusStore.Received(1).WriteStatusAsync(
            "test-tenant",
            "corr-drain",
            Arg.Is<CommandStatusRecord>(r => r.Status == CommandStatus.Completed),
            Arg.Any<CancellationToken>());
    }

    // --- Task 7.5: Drain fails, retry count incremented ---

    [Fact]
    public async Task ReceiveReminder_DrainFails_RetryCountIncremented() {
        // Arrange
        (AggregateActor actor, IActorStateManager stateManager, _, IEventPublisher eventPublisher, _) = CreateActor();
        UnpublishedEventsRecord record = CreateDrainRecord(retryCount: 2);

        _ = stateManager.TryGetStateAsync<UnpublishedEventsRecord>(
            "drain:corr-drain", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<UnpublishedEventsRecord>(true, record));

        ConfigureEventsInState(stateManager, 2);

        _ = eventPublisher.PublishEventsAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<bool>())
            .Returns(new EventPublishResult(false, 0, "Still unavailable"));

        // Act
        await actor.ReceiveReminderAsync("drain-unpublished-corr-drain", [], TimeSpan.Zero, TimeSpan.Zero);

        // Assert
        await stateManager.Received().SetStateAsync(
            "drain:corr-drain",
            Arg.Is<UnpublishedEventsRecord>(r => r.RetryCount == 3 && r.LastFailureReason == "Still unavailable"),
            Arg.Any<CancellationToken>());
    }

    // --- Task 7.6: Drain fails, record preserved ---

    [Fact]
    public async Task ReceiveReminder_DrainFails_RecordPreserved() {
        // Arrange
        (AggregateActor actor, IActorStateManager stateManager, _, IEventPublisher eventPublisher, _) = CreateActor();
        UnpublishedEventsRecord record = CreateDrainRecord();

        _ = stateManager.TryGetStateAsync<UnpublishedEventsRecord>(
            "drain:corr-drain", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<UnpublishedEventsRecord>(true, record));

        ConfigureEventsInState(stateManager, 2);

        _ = eventPublisher.PublishEventsAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<bool>())
            .Returns(new EventPublishResult(false, 0, "unavailable"));

        // Act
        await actor.ReceiveReminderAsync("drain-unpublished-corr-drain", [], TimeSpan.Zero, TimeSpan.Zero);

        // Assert -- record NOT removed (only updated)
        await stateManager.DidNotReceive().RemoveStateAsync(
            "drain:corr-drain", Arg.Any<CancellationToken>());

        // Updated record saved
        await stateManager.Received().SetStateAsync(
            "drain:corr-drain",
            Arg.Is<UnpublishedEventsRecord>(r => r.RetryCount == 1),
            Arg.Any<CancellationToken>());
    }

    // --- Task 7.7: Drain fails, reminder continues ---

    [Fact]
    public async Task ReceiveReminder_DrainFails_LogsWarning() {
        // Arrange
        (AggregateActor actor, IActorStateManager stateManager, ILogger<AggregateActor> logger, IEventPublisher eventPublisher, _) = CreateActor();
        UnpublishedEventsRecord record = CreateDrainRecord();

        _ = stateManager.TryGetStateAsync<UnpublishedEventsRecord>(
            "drain:corr-drain", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<UnpublishedEventsRecord>(true, record));

        ConfigureEventsInState(stateManager, 2);

        _ = eventPublisher.PublishEventsAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<bool>())
            .Returns(new EventPublishResult(false, 0, "unavailable"));

        // Act
        await actor.ReceiveReminderAsync("drain-unpublished-corr-drain", [], TimeSpan.Zero, TimeSpan.Zero);

        // Assert -- warning logged
        logger.Received().Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Drain failed")),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    /// <summary>
    /// Story 4.4 amendment: this test previously encoded UNBOUNDED retry -- any RetryCount kept the
    /// reminder armed. Drain attempts are now capped, so the assertion is only meaningful at the
    /// boundary: the LAST attempt still below <c>MaxDrainAttempts</c> must keep the reminder armed.
    /// The at-cap behavior is pinned separately by the exhaustion tests below.
    /// </summary>
    [Fact]
    public async Task ReceiveReminder_DrainFailsOnLastAttemptBelowCap_ReminderContinuesFiring() {
        // Arrange
        (AggregateActor actor, IActorStateManager stateManager, _, IEventPublisher eventPublisher, _, ActorTimerManager timerManager) = CreateActorWithTimerManager();
        UnpublishedEventsRecord record = CreateDrainRecord(
            retryCount: EventDrainOptions.DefaultMaxDrainAttempts - 1);

        _ = stateManager.TryGetStateAsync<UnpublishedEventsRecord>(
            "drain:corr-drain", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<UnpublishedEventsRecord>(true, record));

        ConfigureEventsInState(stateManager, 2);

        _ = eventPublisher.PublishEventsAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<bool>())
            .Returns(new EventPublishResult(false, 0, "unavailable"));

        // Act
        await actor.ReceiveReminderAsync("drain-unpublished-corr-drain", [], TimeSpan.Zero, TimeSpan.Zero);

        // Assert -- failure path must not unregister reminder
        await timerManager.DidNotReceive().UnregisterReminderAsync(
            Arg.Any<ActorReminderToken>());
    }

    [Fact]
    public async Task ReceiveReminder_DrainPublishFails_ActivityFailureReasonIsStableCode() {
        (AggregateActor actor, IActorStateManager stateManager, _, IEventPublisher eventPublisher, _) = CreateActor();
        UnpublishedEventsRecord record = CreateDrainRecord();
        _ = stateManager.TryGetStateAsync<UnpublishedEventsRecord>(
            "drain:corr-drain", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<UnpublishedEventsRecord>(true, record));
        ConfigureEventsInState(stateManager, 2);
        _ = eventPublisher.PublishEventsAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<bool>())
            .Returns(new EventPublishResult(false, 0, "test-tenant:test-domain:agg-001 unavailable"));

        Activity activity = await CaptureDrainActivityAsync(() =>
            actor.ReceiveReminderAsync("drain-unpublished-corr-drain", [], TimeSpan.Zero, TimeSpan.Zero));

        activity.GetTagItem("eventstore.failure_reason").ShouldBe(DrainReasonCodes.PublishFailed);
        // Negative assertion: high-cardinality leak guard. The publisher's failure message
        // contained the aggregate ActorId; that must never reach the activity tag.
        (activity.GetTagItem("eventstore.failure_reason")?.ToString() ?? string.Empty)
            .ShouldNotContain("test-tenant:test-domain:agg-001");
    }

    [Theory]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(12)]
    public async Task ReceiveReminder_DrainRangeMissingPersistedEvent_PreservesRecordAndDoesNotPublish(int missingSequence) {
        // Arrange
        (AggregateActor actor, IActorStateManager stateManager, _, IEventPublisher eventPublisher, _, ActorTimerManager timerManager) = CreateActorWithTimerManager();
        var record = new UnpublishedEventsRecord(
            CorrelationId: "corr-drain",
            StartSequence: 10,
            EndSequence: 12,
            EventCount: 3,
            CommandType: "CreateOrder",
            IsRejection: false,
            FailedAt: DateTimeOffset.UtcNow,
            RetryCount: 0,
            LastFailureReason: "Pub/sub unavailable");

        _ = stateManager.TryGetStateAsync<UnpublishedEventsRecord>(
            "drain:corr-drain", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<UnpublishedEventsRecord>(true, record));

        ConfigureThreeEventDrainWithMissingSequence(stateManager, missingSequence);

        // Act
        await actor.ReceiveReminderAsync("drain-unpublished-corr-drain", [], TimeSpan.Zero, TimeSpan.Zero);

        // Assert
        await AssertDrainIntegrityFailureAsync(
            stateManager,
            eventPublisher,
            timerManager,
            $"sequence {missingSequence}");
    }

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    public async Task ReceiveReminder_DrainRecordEventCountMismatch_PreservesRecordAndDoesNotPublish(int eventCount) {
        // Arrange
        (AggregateActor actor, IActorStateManager stateManager, _, IEventPublisher eventPublisher, _, ActorTimerManager timerManager) = CreateActorWithTimerManager();
        var record = new UnpublishedEventsRecord(
            CorrelationId: "corr-drain",
            StartSequence: 10,
            EndSequence: 12,
            EventCount: eventCount,
            CommandType: "CreateOrder",
            IsRejection: false,
            FailedAt: DateTimeOffset.UtcNow,
            RetryCount: 0,
            LastFailureReason: "Pub/sub unavailable");

        _ = stateManager.TryGetStateAsync<UnpublishedEventsRecord>(
            "drain:corr-drain", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<UnpublishedEventsRecord>(true, record));

        ConfigureEventsInState(stateManager, eventCount: 3, startSequence: 10);

        // Act
        await actor.ReceiveReminderAsync("drain-unpublished-corr-drain", [], TimeSpan.Zero, TimeSpan.Zero);

        // Assert
        await AssertDrainIntegrityFailureAsync(
            stateManager,
            eventPublisher,
            timerManager,
            "EventCount");
    }

    [Fact]
    public async Task ReceiveReminder_DrainRecordEventCountMismatch_ActivityFailureReasonIsStableCode() {
        (AggregateActor actor, IActorStateManager stateManager, _, _, _, _) = CreateActorWithTimerManager();
        var record = new UnpublishedEventsRecord(
            CorrelationId: "corr-drain",
            StartSequence: 10,
            EndSequence: 12,
            EventCount: 2,
            CommandType: "CreateOrder",
            IsRejection: false,
            FailedAt: DateTimeOffset.UtcNow,
            RetryCount: 0,
            LastFailureReason: "Pub/sub unavailable");
        _ = stateManager.TryGetStateAsync<UnpublishedEventsRecord>(
            "drain:corr-drain", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<UnpublishedEventsRecord>(true, record));
        ConfigureEventsInState(stateManager, eventCount: 3, startSequence: 10);

        Activity activity = await CaptureDrainActivityAsync(() =>
            actor.ReceiveReminderAsync("drain-unpublished-corr-drain", [], TimeSpan.Zero, TimeSpan.Zero));

        activity.GetTagItem("eventstore.failure_reason").ShouldBe(DrainReasonCodes.EventCountMismatch);
        // Negative assertion: high-cardinality leak guard. The integrity exception message
        // contained the aggregate ActorId; that must never reach the activity tag.
        (activity.GetTagItem("eventstore.failure_reason")?.ToString() ?? string.Empty)
            .ShouldNotContain("test-tenant:test-domain:agg-001");
    }

    // --- Task 7.8: Orphaned reminder cleanup ---

    [Fact]
    public async Task ReceiveReminder_RecordNotFound_LogsWarning() {
        // Arrange
        (AggregateActor actor, IActorStateManager stateManager, ILogger<AggregateActor> logger, _, _) = CreateActor();

        _ = stateManager.TryGetStateAsync<UnpublishedEventsRecord>(
            "drain:corr-orphan", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<UnpublishedEventsRecord>(false, default!));

        // Act
        await actor.ReceiveReminderAsync("drain-unpublished-corr-orphan", [], TimeSpan.Zero, TimeSpan.Zero);

        // Assert -- warning about orphaned reminder
        logger.Received().Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("orphaned reminder")),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    // --- Task 7.9: Multiple unpublished drained independently ---

    [Fact]
    public async Task ReceiveReminder_MultipleUnpublished_DrainedIndependently() {
        // Arrange
        (AggregateActor actor, IActorStateManager stateManager, _, IEventPublisher eventPublisher, _) = CreateActor();

        UnpublishedEventsRecord record1 = CreateDrainRecord(correlationId: "corr-1");
        UnpublishedEventsRecord record2 = CreateDrainRecord(correlationId: "corr-2");

        _ = stateManager.TryGetStateAsync<UnpublishedEventsRecord>(
            "drain:corr-1", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<UnpublishedEventsRecord>(true, record1));
        _ = stateManager.TryGetStateAsync<UnpublishedEventsRecord>(
            "drain:corr-2", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<UnpublishedEventsRecord>(true, record2));

        ConfigureEventsInState(stateManager, 2, "corr-1");

        // First drain succeeds
        _ = eventPublisher.PublishEventsAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            "corr-1",
            Arg.Any<CancellationToken>(),
            Arg.Any<bool>())
            .Returns(new EventPublishResult(true, 2, null));

        // Second drain fails
        _ = eventPublisher.PublishEventsAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            "corr-2",
            Arg.Any<CancellationToken>(),
            Arg.Any<bool>())
            .Returns(new EventPublishResult(false, 0, "still down"));

        // Act -- drain first
        await actor.ReceiveReminderAsync("drain-unpublished-corr-1", [], TimeSpan.Zero, TimeSpan.Zero);

        // Assert -- first record removed
        await stateManager.Received().RemoveStateAsync(
            "drain:corr-1", Arg.Any<CancellationToken>());

        // Act -- drain second
        await actor.ReceiveReminderAsync("drain-unpublished-corr-2", [], TimeSpan.Zero, TimeSpan.Zero);

        // Assert -- second record updated, not removed
        await stateManager.DidNotReceive().RemoveStateAsync(
            "drain:corr-2", Arg.Any<CancellationToken>());
        await stateManager.Received().SetStateAsync(
            "drain:corr-2",
            Arg.Is<UnpublishedEventsRecord>(r => r.RetryCount == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReceiveReminder_MultipleUnpublished_UsesRecordedSequenceRangePerCorrelation() {
        // Arrange
        (AggregateActor actor, IActorStateManager stateManager, _, IEventPublisher eventPublisher, _) = CreateActor();

        var record1 = new UnpublishedEventsRecord(
            CorrelationId: "corr-1",
            StartSequence: 1,
            EndSequence: 2,
            EventCount: 2,
            CommandType: "CreateOrder",
            IsRejection: false,
            FailedAt: DateTimeOffset.UtcNow,
            RetryCount: 0,
            LastFailureReason: "unavailable");

        var record2 = new UnpublishedEventsRecord(
            CorrelationId: "corr-2",
            StartSequence: 3,
            EndSequence: 4,
            EventCount: 2,
            CommandType: "CreateOrder",
            IsRejection: false,
            FailedAt: DateTimeOffset.UtcNow,
            RetryCount: 0,
            LastFailureReason: "unavailable");

        _ = stateManager.TryGetStateAsync<UnpublishedEventsRecord>(
            "drain:corr-1", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<UnpublishedEventsRecord>(true, record1));

        _ = stateManager.TryGetStateAsync<UnpublishedEventsRecord>(
            "drain:corr-2", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<UnpublishedEventsRecord>(true, record2));

        ConfigureEventsInState(stateManager, eventCount: 2, correlationId: "corr-1", startSequence: 1);
        ConfigureEventsInState(stateManager, eventCount: 2, correlationId: "corr-2", startSequence: 3);

        var publishedSequences = new Dictionary<string, IReadOnlyList<long>>();
        _ = eventPublisher.PublishEventsAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<bool>())
            .Returns(callInfo => {
                string correlationId = callInfo.ArgAt<string>(2);
                IReadOnlyList<EventEnvelope> events = callInfo.ArgAt<IReadOnlyList<EventEnvelope>>(1);
                publishedSequences[correlationId] = events.Select(e => e.SequenceNumber).ToArray();
                return new EventPublishResult(true, events.Count, null);
            });

        // Act
        await actor.ReceiveReminderAsync("drain-unpublished-corr-1", [], TimeSpan.Zero, TimeSpan.Zero);
        await actor.ReceiveReminderAsync("drain-unpublished-corr-2", [], TimeSpan.Zero, TimeSpan.Zero);

        // Assert
        publishedSequences["corr-1"].ShouldBe([1L, 2L]);
        publishedSequences["corr-2"].ShouldBe([3L, 4L]);
    }

    [Fact]
    public async Task ReceiveReminder_PartialPublishRecovery_RePublishesCompleteRecordedRangeInOrder() {
        // Arrange
        (AggregateActor actor, IActorStateManager stateManager, _, IEventPublisher eventPublisher, _) = CreateActor();
        var record = new UnpublishedEventsRecord(
            CorrelationId: "corr-partial",
            StartSequence: 1,
            EndSequence: 3,
            EventCount: 3,
            CommandType: "CreateOrder",
            IsRejection: false,
            FailedAt: DateTimeOffset.UtcNow,
            RetryCount: 1,
            LastFailureReason: "Connection reset after event 2");

        _ = stateManager.TryGetStateAsync<UnpublishedEventsRecord>(
            "drain:corr-partial", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<UnpublishedEventsRecord>(true, record));

        ConfigureEventsInState(stateManager, eventCount: 3, correlationId: "corr-partial", startSequence: 1);

        IReadOnlyList<long>? publishedSequences = null;
        _ = eventPublisher.PublishEventsAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
            Arg.Do<IReadOnlyList<EventEnvelope>>(events =>
                publishedSequences = events.Select(e => e.SequenceNumber).ToArray()),
            "corr-partial",
            Arg.Any<CancellationToken>(),
            Arg.Any<bool>())
            .Returns(callInfo => new EventPublishResult(true, callInfo.ArgAt<IReadOnlyList<EventEnvelope>>(1).Count, null));

        // Act
        await actor.ReceiveReminderAsync("drain-unpublished-corr-partial", [], TimeSpan.Zero, TimeSpan.Zero);

        // Assert
        _ = publishedSequences.ShouldNotBeNull();
        publishedSequences.ShouldBe([1L, 2L, 3L]);
        await stateManager.Received(1).RemoveStateAsync(
            "drain:corr-partial", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReceiveReminder_ReminderOverlap_SecondCallIsDuplicateTolerant() {
        // Simulates actor restart / reminder overlap: drain record is still present for both calls
        // because cleanup from the first run did not commit before deactivation.
        // Expected: duplicate publish is allowed; cleanup is attempted on both runs.
        (AggregateActor actor, IActorStateManager stateManager, _, IEventPublisher eventPublisher, _) = CreateActor();
        var record = new UnpublishedEventsRecord(
            CorrelationId: "corr-overlap",
            StartSequence: 1,
            EndSequence: 2,
            EventCount: 2,
            CommandType: "CreateOrder",
            IsRejection: false,
            FailedAt: DateTimeOffset.UtcNow,
            RetryCount: 0,
            LastFailureReason: "initial failure");

        _ = stateManager.TryGetStateAsync<UnpublishedEventsRecord>(
            "drain:corr-overlap", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<UnpublishedEventsRecord>(true, record));

        ConfigureEventsInState(stateManager, eventCount: 2, correlationId: "corr-overlap", startSequence: 1);

        int publishCallCount = 0;
        _ = eventPublisher.PublishEventsAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            "corr-overlap",
            Arg.Any<CancellationToken>(),
            Arg.Any<bool>())
            .Returns(callInfo => {
                publishCallCount++;
                return new EventPublishResult(true, callInfo.ArgAt<IReadOnlyList<EventEnvelope>>(1).Count, null);
            });

        // Act — reminder fires twice (overlap or actor restart before cleanup committed)
        await actor.ReceiveReminderAsync("drain-unpublished-corr-overlap", [], TimeSpan.Zero, TimeSpan.Zero);
        await actor.ReceiveReminderAsync("drain-unpublished-corr-overlap", [], TimeSpan.Zero, TimeSpan.Zero);

        // Assert — duplicate publication is expected and allowed; cleanup must be attempted both times
        publishCallCount.ShouldBe(2);
        await stateManager.Received(2).RemoveStateAsync("drain:corr-overlap", Arg.Any<CancellationToken>());
    }

    // --- Task 7.10: Rejection events drained with correct status ---

    [Fact]
    public async Task ReceiveReminder_RejectionEvents_DrainedAndStatusRejected() {
        // Arrange
        (AggregateActor actor, IActorStateManager stateManager, _, IEventPublisher eventPublisher, ICommandStatusStore statusStore) = CreateActor();
        UnpublishedEventsRecord record = CreateDrainRecord(isRejection: true);

        _ = stateManager.TryGetStateAsync<UnpublishedEventsRecord>(
            "drain:corr-drain", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<UnpublishedEventsRecord>(true, record));

        ConfigureEventsInState(stateManager, 2);

        _ = eventPublisher.PublishEventsAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<bool>())
            .Returns(new EventPublishResult(true, 2, null));

        // Act
        await actor.ReceiveReminderAsync("drain-unpublished-corr-drain", [], TimeSpan.Zero, TimeSpan.Zero);

        // Assert -- status is Rejected (not Completed)
        await statusStore.Received(1).WriteStatusAsync(
            "test-tenant",
            "corr-drain",
            Arg.Is<CommandStatusRecord>(r => r.Status == CommandStatus.Rejected),
            Arg.Any<CancellationToken>());
    }

    // --- Task 7.11: Unknown reminder ignored ---

    [Fact]
    public async Task ReceiveReminder_UnknownReminder_Ignored() {
        // Arrange
        (AggregateActor actor, _, ILogger<AggregateActor> logger, IEventPublisher eventPublisher, _) = CreateActor();

        // Act
        await actor.ReceiveReminderAsync("some-other-reminder", [], TimeSpan.Zero, TimeSpan.Zero);

        // Assert -- event publisher NOT called
        _ = await eventPublisher.DidNotReceive().PublishEventsAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<bool>());

        // Warning logged
        logger.Received().Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Unknown reminder")),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    // --- Task 9.2: Full drain cycle ---

    [Fact]
    public async Task FullDrainCycle_PublishFails_ThenDrainSucceeds_EventsDelivered() {
        // Arrange -- simulate full cycle: command → publish fails → drain succeeds
        (AggregateActor actor, IActorStateManager stateManager, _, IEventPublisher eventPublisher, _) = CreateActor();
        UnpublishedEventsRecord record = CreateDrainRecord(retryCount: 0);

        _ = stateManager.TryGetStateAsync<UnpublishedEventsRecord>(
            "drain:corr-drain", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<UnpublishedEventsRecord>(true, record));

        ConfigureEventsInState(stateManager, 2);

        // Now pub/sub is back
        _ = eventPublisher.PublishEventsAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<bool>())
            .Returns(new EventPublishResult(true, 2, null));

        // Act
        await actor.ReceiveReminderAsync("drain-unpublished-corr-drain", [], TimeSpan.Zero, TimeSpan.Zero);

        // Assert -- events delivered
        _ = await eventPublisher.Received(1).PublishEventsAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
            Arg.Is<IReadOnlyList<EventEnvelope>>(e => e.Count == 2),
            "corr-drain",
            Arg.Any<CancellationToken>(),
            Arg.Any<bool>());

        // Record removed
        await stateManager.Received(1).RemoveStateAsync(
            "drain:corr-drain", Arg.Any<CancellationToken>());
    }

    // --- Task 9.3: Multiple drain failures then success ---

    [Fact]
    public async Task FullDrainCycle_MultipleFails_ThenSuccess_RetryCountAccurate() {
        // Arrange -- record with 3 prior failures
        (AggregateActor actor, IActorStateManager stateManager, _, IEventPublisher eventPublisher, _) = CreateActor();
        UnpublishedEventsRecord record = CreateDrainRecord(retryCount: 3);

        _ = stateManager.TryGetStateAsync<UnpublishedEventsRecord>(
            "drain:corr-drain", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<UnpublishedEventsRecord>(true, record));

        ConfigureEventsInState(stateManager, 2);

        _ = eventPublisher.PublishEventsAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<bool>())
            .Returns(new EventPublishResult(true, 2, null));

        // Act
        await actor.ReceiveReminderAsync("drain-unpublished-corr-drain", [], TimeSpan.Zero, TimeSpan.Zero);

        // Assert -- success despite previous retries
        await stateManager.Received(1).RemoveStateAsync(
            "drain:corr-drain", Arg.Any<CancellationToken>());
    }

    // --- Task 9.4: Events identical after drain ---

    [Fact]
    public async Task FullDrainCycle_EventsSameAsOriginal_NoDataLoss() {
        // Arrange
        (AggregateActor actor, IActorStateManager stateManager, _, IEventPublisher eventPublisher, _) = CreateActor();
        UnpublishedEventsRecord record = CreateDrainRecord();

        _ = stateManager.TryGetStateAsync<UnpublishedEventsRecord>(
            "drain:corr-drain", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<UnpublishedEventsRecord>(true, record));

        ConfigureEventsInState(stateManager, 2);

        IReadOnlyList<EventEnvelope>? publishedEvents = null;
        _ = eventPublisher.PublishEventsAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
            Arg.Do<IReadOnlyList<EventEnvelope>>(e => publishedEvents = e),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<bool>())
            .Returns(new EventPublishResult(true, 2, null));

        // Act
        await actor.ReceiveReminderAsync("drain-unpublished-corr-drain", [], TimeSpan.Zero, TimeSpan.Zero);

        // Assert -- events have correct sequence numbers
        _ = publishedEvents.ShouldNotBeNull();
        publishedEvents.Count.ShouldBe(2);
        publishedEvents[0].SequenceNumber.ShouldBe(1);
        publishedEvents[1].SequenceNumber.ShouldBe(2);
        publishedEvents[0].AggregateId.ShouldBe("agg-001");
        publishedEvents[1].AggregateId.ShouldBe("agg-001");
    }

    // --- Task 9.5: Topic correct after drain ---

    [Fact]
    public async Task FullDrainCycle_TopicCorrect_MatchesOriginalPublication() {
        // Arrange
        (AggregateActor actor, IActorStateManager stateManager, _, IEventPublisher eventPublisher, _) = CreateActor();
        UnpublishedEventsRecord record = CreateDrainRecord();

        _ = stateManager.TryGetStateAsync<UnpublishedEventsRecord>(
            "drain:corr-drain", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<UnpublishedEventsRecord>(true, record));

        ConfigureEventsInState(stateManager, 2);

        Hexalith.EventStore.Contracts.Identity.AggregateIdentity? publishedIdentity = null;
        _ = eventPublisher.PublishEventsAsync(
            Arg.Do<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(id => publishedIdentity = id),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<bool>())
            .Returns(new EventPublishResult(true, 2, null));

        // Act
        await actor.ReceiveReminderAsync("drain-unpublished-corr-drain", [], TimeSpan.Zero, TimeSpan.Zero);

        // Assert -- identity reconstructed correctly from actor ID
        _ = publishedIdentity.ShouldNotBeNull();
        publishedIdentity.TenantId.ShouldBe("test-tenant");
        publishedIdentity.Domain.ShouldBe("test-domain");
        publishedIdentity.AggregateId.ShouldBe("agg-001");
        publishedIdentity.PubSubTopic.ShouldBe("test-tenant.test-domain.events");
    }

    // ===================================================================================
    // Story 4.4: bounded drain attempts, exhaustion dead-lettering, the fail-closed index
    // capacity branch, drain telemetry identity, and the Recoverable -> Terminal transition.
    // ===================================================================================

    private const string ExhaustedTrackingId = "msg-exhausted";
    private const string ExhaustedCorrelationId = "corr-exhausted";

    private sealed record BoundedDrainContext(
        AggregateActor Actor,
        IActorStateManager StateManager,
        IEventPublisher EventPublisher,
        ICommandStatusStore StatusStore,
        IDeadLetterPublisher DeadLetterPublisher,
        ActorTimerManager TimerManager,
        IDomainServiceInvoker Invoker);

    private static BoundedDrainContext CreateActorForBoundedDrain(EventDrainOptions? drainOptions = null) {
        IActorStateManager stateManager = Substitute.For<IActorStateManager>();
        ILogger<AggregateActor> logger = Substitute.For<ILogger<AggregateActor>>();
        IDomainServiceInvoker invoker = Substitute.For<IDomainServiceInvoker>();
        ISnapshotManager snapshotManager = Substitute.For<ISnapshotManager>();
        ICommandStatusStore statusStore = Substitute.For<ICommandStatusStore>();
        IEventPublisher eventPublisher = Substitute.For<IEventPublisher>();
        IDeadLetterPublisher deadLetterPublisher = Substitute.For<IDeadLetterPublisher>();
        ActorTimerManager timerManager = Substitute.For<ActorTimerManager>();
        var host = ActorHost.CreateForTest<AggregateActor>(
            new ActorTestOptions {
                ActorId = new ActorId("test-tenant:test-domain:agg-001"),
                TimerManager = timerManager,
            });
        var actor = new AggregateActor(
            host,
            logger,
            invoker,
            snapshotManager,
            new NoOpEventPayloadProtectionService(),
            statusStore,
            eventPublisher,
            Options.Create(drainOptions ?? new EventDrainOptions()),
            Options.Create(new BackpressureOptions()),
            deadLetterPublisher);
        ActorStateManagerTestHelper.SetStateManager(actor, stateManager);

        _ = deadLetterPublisher.PublishDeadLetterAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
            Arg.Any<DeadLetterMessage>(),
            Arg.Any<CancellationToken>())
            .Returns(true);

        return new BoundedDrainContext(
            actor, stateManager, eventPublisher, statusStore, deadLetterPublisher, timerManager, invoker);
    }

    private static UnpublishedEventsRecord SeedExhaustedRecord(
        BoundedDrainContext context,
        int retryCount,
        bool deadLettered = false,
        int pendingCommandCount = 1) {
        var record = new UnpublishedEventsRecord(
            ExhaustedCorrelationId,
            StartSequence: 1,
            EndSequence: 2,
            EventCount: 2,
            CommandType: "CreateOrder",
            IsRejection: false,
            FailedAt: DateTimeOffset.UtcNow,
            RetryCount: retryCount,
            LastFailureReason: "Pub/sub unavailable",
            MessageId: ExhaustedTrackingId,
            DeadLettered: deadLettered);

        _ = context.StateManager.TryGetStateAsync<UnpublishedEventsRecord>(
            $"drain:{ExhaustedTrackingId}", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<UnpublishedEventsRecord>(true, record));
        _ = context.StateManager.TryGetStateAsync<int>(
            "pending_command_count", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<int>(true, pendingCommandCount));
        _ = context.StateManager.TryGetStateAsync<UnpublishedPublicationIndex>(
            UnpublishedPublicationIndex.StateKey, Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<UnpublishedPublicationIndex>(
                true,
                new UnpublishedPublicationIndex([
                    new UnpublishedPublicationEntry(ExhaustedTrackingId, ExhaustedCorrelationId, DateTimeOffset.UtcNow),
                ])));

        ConfigureEventsInState(context.StateManager, eventCount: 2, correlationId: ExhaustedCorrelationId);
        return record;
    }

    private static void SeedRecoverableIdempotencyRecord(BoundedDrainContext context, string messageId) {
        var record = new IdempotencyRecord(
            CausationId: messageId,
            CorrelationId: ExhaustedCorrelationId,
            Accepted: true,
            ErrorMessage: null,
            ProcessedAt: DateTimeOffset.UtcNow.AddHours(-1),
            EventCount: 2,
            MessageId: messageId,
            CommandType: "CreateOrder",
            ExpiresAt: DateTimeOffset.UtcNow.AddHours(23),
            Disposition: IdempotencyRecordDisposition.Recoverable);
        _ = context.StateManager.TryGetStateAsync<IdempotencyRecord>(
            $"idempotency:{messageId}", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<IdempotencyRecord>(true, record));
    }

    [Fact]
    public async Task ReceiveReminder_AttemptsExhausted_DoesNotPublishAndDeadLettersTheRange() {
        BoundedDrainContext ctx = CreateActorForBoundedDrain();
        _ = SeedExhaustedRecord(ctx, EventDrainOptions.DefaultMaxDrainAttempts);

        await ctx.Actor.ReceiveReminderAsync($"drain-unpublished-{ExhaustedTrackingId}", [], TimeSpan.Zero, TimeSpan.Zero);

        _ = await ctx.EventPublisher.DidNotReceive().PublishEventsAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<bool>());
        _ = await ctx.DeadLetterPublisher.Received(1).PublishDeadLetterAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
            Arg.Is<DeadLetterMessage>(m =>
                !m.ReplayEligible
                && m.ReasonCode == DrainReasonCodes.AttemptsExhausted
                && m.CorrelationId == ExhaustedCorrelationId
                && m.Command.MessageId == ExhaustedTrackingId
                && m.StartSequence == 1
                && m.EndSequence == 2
                && m.DrainAttempts == EventDrainOptions.DefaultMaxDrainAttempts),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReceiveReminder_AttemptsExhausted_MarksRecordDeadLetteredBeforeRemovingIt() {
        BoundedDrainContext ctx = CreateActorForBoundedDrain();
        _ = SeedExhaustedRecord(ctx, EventDrainOptions.DefaultMaxDrainAttempts);
        var calls = new List<string>();
        ctx.StateManager
            .When(s => s.SetStateAsync(
                $"drain:{ExhaustedTrackingId}",
                Arg.Is<UnpublishedEventsRecord>(r => r.DeadLettered),
                Arg.Any<CancellationToken>()))
            .Do(_ => calls.Add("mark"));
        ctx.StateManager
            .When(s => s.RemoveStateAsync($"drain:{ExhaustedTrackingId}", Arg.Any<CancellationToken>()))
            .Do(_ => calls.Add("remove"));

        // The SaveStateAsync between them is the load-bearing step: staging order alone proves
        // nothing, because an unflushed mark is not durable and a fault would replay the publish.
        ctx.StateManager.When(s => s.SaveStateAsync(Arg.Any<CancellationToken>())).Do(_ => calls.Add("save"));

        await ctx.Actor.ReceiveReminderAsync($"drain-unpublished-{ExhaustedTrackingId}", [], TimeSpan.Zero, TimeSpan.Zero);

        // The durable dead-letter mark must be COMMITTED before the post-publish mutations so a
        // fault between them cannot dead-letter the same committed range twice.
        int mark = calls.IndexOf("mark");
        int remove = calls.IndexOf("remove");
        mark.ShouldBeGreaterThanOrEqualTo(0);
        remove.ShouldBeGreaterThan(mark);
        calls.GetRange(mark, remove - mark).ShouldContain("save");
    }

    [Fact]
    public async Task ReceiveReminder_AttemptsExhausted_RemovesRecordEntryReminderAndPendingSlot() {
        BoundedDrainContext ctx = CreateActorForBoundedDrain();
        _ = SeedExhaustedRecord(ctx, EventDrainOptions.DefaultMaxDrainAttempts);

        await ctx.Actor.ReceiveReminderAsync($"drain-unpublished-{ExhaustedTrackingId}", [], TimeSpan.Zero, TimeSpan.Zero);

        await ctx.StateManager.Received(1).RemoveStateAsync(
            $"drain:{ExhaustedTrackingId}", Arg.Any<CancellationToken>());
        await ctx.StateManager.Received(1).SetStateAsync(
            UnpublishedPublicationIndex.StateKey,
            Arg.Is<UnpublishedPublicationIndex>(i => !i.Contains(ExhaustedTrackingId)),
            Arg.Any<CancellationToken>());
        await ctx.StateManager.Received(1).SetStateAsync(
            "pending_command_count", 0, Arg.Any<CancellationToken>());
        await ctx.TimerManager.Received(1).UnregisterReminderAsync(Arg.Any<ActorReminderToken>());
    }

    [Fact]
    public async Task ReceiveReminder_AttemptsExhausted_StatusExposesNonRetryableExhaustionCode() {
        BoundedDrainContext ctx = CreateActorForBoundedDrain();
        _ = SeedExhaustedRecord(ctx, EventDrainOptions.DefaultMaxDrainAttempts);

        await ctx.Actor.ReceiveReminderAsync($"drain-unpublished-{ExhaustedTrackingId}", [], TimeSpan.Zero, TimeSpan.Zero);

        await ctx.StatusStore.Received(1).WriteStatusAsync(
            "test-tenant",
            ExhaustedTrackingId,
            Arg.Is<CommandStatusRecord>(r =>
                r.Status == CommandStatus.PublishFailed
                && r.Retryable == false
                && r.RecoveryReasonCode == DrainReasonCodes.AttemptsExhausted
                && r.DrainAttemptCount == EventDrainOptions.DefaultMaxDrainAttempts),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReceiveReminder_AttemptsExhaustedAndDeadLetterSinkFails_RetainsRecordEntryAndReminder() {
        BoundedDrainContext ctx = CreateActorForBoundedDrain();
        _ = SeedExhaustedRecord(ctx, EventDrainOptions.DefaultMaxDrainAttempts);
        _ = ctx.DeadLetterPublisher.PublishDeadLetterAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
            Arg.Any<DeadLetterMessage>(),
            Arg.Any<CancellationToken>())
            .Returns(false);

        await ctx.Actor.ReceiveReminderAsync($"drain-unpublished-{ExhaustedTrackingId}", [], TimeSpan.Zero, TimeSpan.Zero);

        await ctx.StateManager.DidNotReceive().RemoveStateAsync(
            $"drain:{ExhaustedTrackingId}", Arg.Any<CancellationToken>());
        await ctx.StateManager.DidNotReceive().SetStateAsync(
            $"drain:{ExhaustedTrackingId}",
            Arg.Is<UnpublishedEventsRecord>(r => r.DeadLettered),
            Arg.Any<CancellationToken>());
        await ctx.StateManager.DidNotReceive().SetStateAsync(
            UnpublishedPublicationIndex.StateKey,
            Arg.Any<UnpublishedPublicationIndex>(),
            Arg.Any<CancellationToken>());
        await ctx.StateManager.DidNotReceive().SetStateAsync(
            "pending_command_count", Arg.Any<int>(), Arg.Any<CancellationToken>());
        await ctx.TimerManager.DidNotReceive().UnregisterReminderAsync(Arg.Any<ActorReminderToken>());
    }

    [Fact]
    public async Task ReceiveReminder_AlreadyDeadLetteredRecord_DoesNotDeadLetterTheSameRangeTwice() {
        BoundedDrainContext ctx = CreateActorForBoundedDrain();
        _ = SeedExhaustedRecord(ctx, EventDrainOptions.DefaultMaxDrainAttempts, deadLettered: true);

        await ctx.Actor.ReceiveReminderAsync($"drain-unpublished-{ExhaustedTrackingId}", [], TimeSpan.Zero, TimeSpan.Zero);

        _ = await ctx.DeadLetterPublisher.DidNotReceive().PublishDeadLetterAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
            Arg.Any<DeadLetterMessage>(),
            Arg.Any<CancellationToken>());

        // The interrupted post-publish mutations still complete, exactly once.
        await ctx.StateManager.Received(1).RemoveStateAsync(
            $"drain:{ExhaustedTrackingId}", Arg.Any<CancellationToken>());
        await ctx.StateManager.Received(1).SetStateAsync(
            "pending_command_count", 0, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReceiveReminder_OneAttemptBelowCap_StillPublishes() {
        BoundedDrainContext ctx = CreateActorForBoundedDrain();
        _ = SeedExhaustedRecord(ctx, EventDrainOptions.DefaultMaxDrainAttempts - 1);
        _ = ctx.EventPublisher.PublishEventsAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<bool>())
            .Returns(new EventPublishResult(true, 2, null));

        await ctx.Actor.ReceiveReminderAsync($"drain-unpublished-{ExhaustedTrackingId}", [], TimeSpan.Zero, TimeSpan.Zero);

        _ = await ctx.EventPublisher.Received(1).PublishEventsAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            ExhaustedCorrelationId,
            Arg.Any<CancellationToken>(),
            Arg.Any<bool>());
        _ = await ctx.DeadLetterPublisher.DidNotReceive().PublishDeadLetterAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
            Arg.Any<DeadLetterMessage>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReceiveReminder_ConfiguredCapHonored_ExhaustsAtTheConfiguredBound() {
        BoundedDrainContext ctx = CreateActorForBoundedDrain(new EventDrainOptions { MaxDrainAttempts = 2 });
        _ = SeedExhaustedRecord(ctx, retryCount: 2);

        await ctx.Actor.ReceiveReminderAsync($"drain-unpublished-{ExhaustedTrackingId}", [], TimeSpan.Zero, TimeSpan.Zero);

        _ = await ctx.DeadLetterPublisher.Received(1).PublishDeadLetterAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
            Arg.Is<DeadLetterMessage>(m => m.DrainAttempts == 2),
            Arg.Any<CancellationToken>());
        _ = await ctx.EventPublisher.DidNotReceive().PublishEventsAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<bool>());
    }

    // --- P3: index-entry release on drain success ---

    [Fact]
    public async Task ReceiveReminder_DrainSucceeds_ReleasesThePublicationIndexEntry() {
        // Without this release every recovered command leaks an entry, and with the fail-closed
        // capacity branch a leaking index eventually rejects legitimate commands.
        BoundedDrainContext ctx = CreateActorForBoundedDrain();
        _ = SeedExhaustedRecord(ctx, retryCount: 0);
        _ = ctx.EventPublisher.PublishEventsAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<bool>())
            .Returns(new EventPublishResult(true, 2, null));

        await ctx.Actor.ReceiveReminderAsync($"drain-unpublished-{ExhaustedTrackingId}", [], TimeSpan.Zero, TimeSpan.Zero);

        await ctx.StateManager.Received(1).SetStateAsync(
            UnpublishedPublicationIndex.StateKey,
            Arg.Is<UnpublishedPublicationIndex>(i => !i.Contains(ExhaustedTrackingId)),
            Arg.Any<CancellationToken>());
    }

    // --- P4: retryability at the producing site, below the cap and on success ---

    [Fact]
    public async Task ReceiveReminder_DrainFailsBelowCap_StatusExposesRetryableWithAttemptCount() {
        BoundedDrainContext ctx = CreateActorForBoundedDrain();
        _ = SeedExhaustedRecord(ctx, retryCount: 2);
        _ = ctx.EventPublisher.PublishEventsAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<bool>())
            .Returns(new EventPublishResult(false, 0, "still unavailable"));

        await ctx.Actor.ReceiveReminderAsync($"drain-unpublished-{ExhaustedTrackingId}", [], TimeSpan.Zero, TimeSpan.Zero);

        await ctx.StatusStore.Received(1).WriteStatusAsync(
            "test-tenant",
            ExhaustedTrackingId,
            Arg.Is<CommandStatusRecord>(r =>
                r.Status == CommandStatus.PublishFailed
                && r.Retryable == true
                && r.RecoveryReasonCode == DrainReasonCodes.PublishFailed
                && r.DrainAttemptCount == 3),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReceiveReminder_DrainThrowsBelowCap_StatusExposesRetryableWithClassifiedReason() {
        BoundedDrainContext ctx = CreateActorForBoundedDrain();
        _ = SeedExhaustedRecord(ctx, retryCount: 1);
        _ = ctx.EventPublisher.PublishEventsAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<bool>())
            .Returns(Task.FromException<EventPublishResult>(new Dapr.DaprException("pubsub unavailable")));

        await ctx.Actor.ReceiveReminderAsync($"drain-unpublished-{ExhaustedTrackingId}", [], TimeSpan.Zero, TimeSpan.Zero);

        await ctx.StatusStore.Received(1).WriteStatusAsync(
            "test-tenant",
            ExhaustedTrackingId,
            Arg.Is<CommandStatusRecord>(r =>
                r.Status == CommandStatus.PublishFailed
                && r.Retryable == true
                && r.RecoveryReasonCode == DrainReasonCodes.PublishFailed
                && r.DrainAttemptCount == 2),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReceiveReminder_DrainFailureReachesTheCap_StatusAlreadyReportsNonRetryable() {
        // P9 boundary: this attempt raises RetryCount to exactly MaxDrainAttempts, so the next
        // firing only dead-letters. Promising one more try here would be a lie.
        BoundedDrainContext ctx = CreateActorForBoundedDrain(new EventDrainOptions { MaxDrainAttempts = 3 });
        _ = SeedExhaustedRecord(ctx, retryCount: 2);
        _ = ctx.EventPublisher.PublishEventsAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<bool>())
            .Returns(new EventPublishResult(false, 0, "still unavailable"));

        await ctx.Actor.ReceiveReminderAsync($"drain-unpublished-{ExhaustedTrackingId}", [], TimeSpan.Zero, TimeSpan.Zero);

        await ctx.StatusStore.Received(1).WriteStatusAsync(
            "test-tenant",
            ExhaustedTrackingId,
            Arg.Is<CommandStatusRecord>(r =>
                r.Retryable == false
                && r.RecoveryReasonCode == DrainReasonCodes.AttemptsExhausted
                && r.DrainAttemptCount == 3),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReceiveReminder_DrainSucceeds_StatusExposesNonRetryableCompletionWithAttemptCount() {
        BoundedDrainContext ctx = CreateActorForBoundedDrain();
        _ = SeedExhaustedRecord(ctx, retryCount: 4);
        _ = ctx.EventPublisher.PublishEventsAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<bool>())
            .Returns(new EventPublishResult(true, 2, null));

        await ctx.Actor.ReceiveReminderAsync($"drain-unpublished-{ExhaustedTrackingId}", [], TimeSpan.Zero, TimeSpan.Zero);

        await ctx.StatusStore.Received(1).WriteStatusAsync(
            "test-tenant",
            ExhaustedTrackingId,
            Arg.Is<CommandStatusRecord>(r =>
                r.Status == CommandStatus.Completed
                && r.Retryable == false
                && r.RecoveryReasonCode == null
                && r.DrainAttemptCount == 4),
            Arg.Any<CancellationToken>());
    }

    // --- P1: AC3 at the two PublishFailed producing sites ---

    [Fact]
    public async Task ProcessCommand_PublishFailedWithDrainArmed_StatusReportsRetryableNotUnknown() {
        BoundedDrainContext ctx = CreateActorForBoundedDrain();
        ConfigureFreshCommandState(ctx);
        _ = ctx.EventPublisher.PublishEventsAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<bool>())
            .Returns(new EventPublishResult(false, 0, "Pub/sub unavailable"));

        _ = await ctx.Actor.ProcessCommandAsync(CreateCapacityEnvelope());

        // Retryable = null would tell a polling client "legacy record", not "a retry is coming".
        await ctx.StatusStore.Received(1).WriteStatusAsync(
            "test-tenant",
            "msg-capacity",
            Arg.Is<CommandStatusRecord>(r =>
                r.Status == CommandStatus.PublishFailed
                && r.Retryable == true
                && r.RecoveryReasonCode == DrainReasonCodes.PublishFailed
                && r.DrainAttemptCount == 0),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResumePublishFailed_WithDrainArmed_StatusReportsRetryableNotUnknown() {
        BoundedDrainContext ctx = CreateActorForBoundedDrain();
        ConfigureFreshCommandState(ctx);
        _ = ctx.StateManager.TryGetStateAsync<PipelineState>(
            Arg.Is<string>(s => s.Contains(":pipeline:corr-capacity", StringComparison.Ordinal)),
            Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<PipelineState>(true, new PipelineState(
                "corr-capacity",
                CommandStatus.EventsStored,
                "CreateOrder",
                DateTimeOffset.UtcNow.AddSeconds(-5),
                EventCount: 2,
                RejectionEventType: null,
                MessageId: "msg-capacity",
                CausationId: "msg-capacity",
                StartSequence: 1,
                EndSequence: 2)));
        ConfigureEventsInState(ctx.StateManager, eventCount: 2, correlationId: "corr-capacity");
        _ = ctx.EventPublisher.PublishEventsAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<bool>())
            .Returns(new EventPublishResult(false, 0, "Pub/sub unavailable"));

        _ = await ctx.Actor.ProcessCommandAsync(CreateCapacityEnvelope());

        // Proves this is the RESUME producing site, not the first-pass one: resume never re-invokes
        // the domain because the events are already committed.
        _ = await ctx.Invoker.DidNotReceive().InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>());

        await ctx.StatusStore.Received(1).WriteStatusAsync(
            "test-tenant",
            "msg-capacity",
            Arg.Is<CommandStatusRecord>(r =>
                r.Status == CommandStatus.PublishFailed
                && r.Retryable == true
                && r.RecoveryReasonCode == DrainReasonCodes.PublishFailed
                && r.DrainAttemptCount == 0),
            Arg.Any<CancellationToken>());
    }

    // --- PR3: retryability must reflect the recovery entry, not just reminder registration ---

    [Fact]
    public async Task ProcessCommand_ReminderRegistrationFailsButEntryTracked_StillReportsRetryable() {
        // Registration failed, but the publication-recovery entry was staged in the commit batch, so
        // the next activation WILL re-arm and publish. Reporting false here tells a polling client
        // to abandon a command the platform is still going to deliver.
        BoundedDrainContext ctx = CreateActorForBoundedDrain();
        ConfigureFreshCommandState(ctx);
        _ = ctx.EventPublisher.PublishEventsAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<bool>())
            .Returns(new EventPublishResult(false, 0, "Pub/sub unavailable"));
        _ = ctx.TimerManager.RegisterReminderAsync(Arg.Any<ActorReminder>())
            .Returns(Task.FromException(new InvalidOperationException("reminder store unavailable")));

        _ = await ctx.Actor.ProcessCommandAsync(CreateCapacityEnvelope());

        await ctx.StatusStore.Received(1).WriteStatusAsync(
            "test-tenant",
            "msg-capacity",
            Arg.Is<CommandStatusRecord>(r =>
                r.Status == CommandStatus.PublishFailed && r.Retryable == true),
            Arg.Any<CancellationToken>());
    }

    // --- PR4: the resume path reports (and preserves) the real attempt count ---

    [Fact]
    public async Task ResumePublishFailed_ExistingDrainRecord_CarriesForwardAndReportsItsAttemptCount() {
        // Resume can run repeatedly for one committed range. Writing RetryCount 0 each time would
        // hand the range a fresh budget on every resume, so MaxDrainAttempts would never be reached
        // and the bound would not be a bound; reporting 0 would understate it to operators too.
        BoundedDrainContext ctx = CreateActorForBoundedDrain();
        ConfigureFreshCommandState(ctx);
        _ = ctx.StateManager.TryGetStateAsync<PipelineState>(
            Arg.Is<string>(s => s.Contains(":pipeline:corr-capacity", StringComparison.Ordinal)),
            Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<PipelineState>(true, new PipelineState(
                "corr-capacity",
                CommandStatus.EventsStored,
                "CreateOrder",
                DateTimeOffset.UtcNow.AddSeconds(-5),
                EventCount: 2,
                RejectionEventType: null,
                MessageId: "msg-capacity",
                CausationId: "msg-capacity",
                StartSequence: 1,
                EndSequence: 2)));
        ConfigureEventsInState(ctx.StateManager, eventCount: 2, correlationId: "corr-capacity");
        _ = ctx.StateManager.TryGetStateAsync<UnpublishedEventsRecord>(
            "drain:msg-capacity", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<UnpublishedEventsRecord>(true, new UnpublishedEventsRecord(
                "corr-capacity", 1, 2, 2, "CreateOrder", false, DateTimeOffset.UtcNow.AddMinutes(-5),
                RetryCount: 5, LastFailureReason: "pubsub down", MessageId: "msg-capacity")));
        _ = ctx.EventPublisher.PublishEventsAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<bool>())
            .Returns(new EventPublishResult(false, 0, "Pub/sub unavailable"));

        _ = await ctx.Actor.ProcessCommandAsync(CreateCapacityEnvelope());

        await ctx.StatusStore.Received(1).WriteStatusAsync(
            "test-tenant",
            "msg-capacity",
            Arg.Is<CommandStatusRecord>(r =>
                r.Status == CommandStatus.PublishFailed && r.DrainAttemptCount == 5),
            Arg.Any<CancellationToken>());

        // And the persisted record keeps the budget rather than resetting it.
        await ctx.StateManager.Received().SetStateAsync(
            "drain:msg-capacity",
            Arg.Is<UnpublishedEventsRecord>(r => r.RetryCount == 5),
            Arg.Any<CancellationToken>());
    }

    // --- PR7b: PublicationIndexAddOutcome.InvalidEntry is UNREACHABLE through the actor ---
    // No actor-level test is added here because none can be written without faking the guard.
    // TryStagePublicationIndexEntryAsync is only ever called with (command.MessageId,
    // command.CorrelationId), and an entry is malformed only when one of those is blank. Both are
    // rejected strictly upstream of the staging site (AggregateActor.cs ~line 645):
    //   - AggregateActor.cs:261 idempotencyChecker.CheckAsync -> CommandProcessingIdentity.Validate
    //     -> ArgumentException.ThrowIfNullOrWhiteSpace(MessageId)
    //   - AggregateActor.cs:306 stateMachine.LoadPipelineStateAsync
    //     -> ArgumentException.ThrowIfNullOrWhiteSpace(correlationId)
    // A blank correlation id therefore throws in LoadPipelineStateAsync, never reaching the branch.
    // The distinction is still pinned where it IS reachable: UnpublishedPublicationIndexTests
    // .TryAdd_MalformedEntry_IsRefused asserts the outcome at the type level. The defensive
    // production branch is retained because TryAdd is a public API whose contract must hold.

    // --- Recoverable -> Terminal disposition transition ---

    [Fact]
    public async Task ReceiveReminder_DrainSucceeds_TransitionsRecoverableIdempotencyRecordToTerminal() {
        BoundedDrainContext ctx = CreateActorForBoundedDrain();
        _ = SeedExhaustedRecord(ctx, retryCount: 0);
        SeedRecoverableIdempotencyRecord(ctx, ExhaustedTrackingId);
        _ = ctx.EventPublisher.PublishEventsAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<bool>())
            .Returns(new EventPublishResult(true, 2, null));

        await ctx.Actor.ReceiveReminderAsync($"drain-unpublished-{ExhaustedTrackingId}", [], TimeSpan.Zero, TimeSpan.Zero);

        await ctx.StateManager.Received(1).SetStateAsync(
            $"idempotency:{ExhaustedTrackingId}",
            Arg.Is<IdempotencyRecord>(r => r.Disposition == IdempotencyRecordDisposition.Terminal),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReceiveReminder_AttemptsExhausted_TransitionsRecoverableIdempotencyRecordToTerminal() {
        BoundedDrainContext ctx = CreateActorForBoundedDrain();
        _ = SeedExhaustedRecord(ctx, EventDrainOptions.DefaultMaxDrainAttempts);
        SeedRecoverableIdempotencyRecord(ctx, ExhaustedTrackingId);

        await ctx.Actor.ReceiveReminderAsync($"drain-unpublished-{ExhaustedTrackingId}", [], TimeSpan.Zero, TimeSpan.Zero);

        await ctx.StateManager.Received(1).SetStateAsync(
            $"idempotency:{ExhaustedTrackingId}",
            Arg.Is<IdempotencyRecord>(r => r.Disposition == IdempotencyRecordDisposition.Terminal),
            Arg.Any<CancellationToken>());
    }

    // --- Drain activity identity tags ---

    [Fact]
    public async Task ReceiveReminder_DrainRuns_ActivityMessageIdComesFromTheRecordNotTheReminderSuffix() {
        BoundedDrainContext ctx = CreateActorForBoundedDrain();
        _ = SeedExhaustedRecord(ctx, retryCount: 0);
        _ = ctx.EventPublisher.PublishEventsAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<bool>())
            .Returns(new EventPublishResult(true, 2, null));

        Activity activity = await CaptureDrainActivityAsync(
            ExhaustedCorrelationId,
            () => ctx.Actor.ReceiveReminderAsync(
                $"drain-unpublished-{ExhaustedTrackingId}", [], TimeSpan.Zero, TimeSpan.Zero));

        activity.GetTagItem("eventstore.message_id").ShouldBe(ExhaustedTrackingId);
        activity.GetTagItem("eventstore.drain_tracking_id").ShouldBe(ExhaustedTrackingId);
    }

    [Fact]
    public async Task ReceiveReminder_LegacyCorrelationKeyedRecord_DoesNotClaimACorrelationIdIsAMessageId() {
        // A legacy record has no MessageId, so the reminder suffix is a correlation id. The
        // eventstore.message_id tag must stay unset rather than mislabel it.
        (AggregateActor actor, IActorStateManager stateManager, _, IEventPublisher eventPublisher, _) = CreateActor();
        UnpublishedEventsRecord record = CreateDrainRecord();
        _ = stateManager.TryGetStateAsync<UnpublishedEventsRecord>(
            "drain:corr-drain", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<UnpublishedEventsRecord>(true, record));
        ConfigureEventsInState(stateManager, 2);
        _ = eventPublisher.PublishEventsAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<bool>())
            .Returns(new EventPublishResult(true, 2, null));

        Activity activity = await CaptureDrainActivityAsync(() =>
            actor.ReceiveReminderAsync("drain-unpublished-corr-drain", [], TimeSpan.Zero, TimeSpan.Zero));

        activity.GetTagItem("eventstore.message_id").ShouldBeNull();
        activity.GetTagItem("eventstore.drain_tracking_id").ShouldBe("corr-drain");
    }

    // --- Actor-level fail-closed capacity branch ---

    [Fact]
    public async Task ProcessCommand_PublicationIndexAtCapacity_DiscardsStagedEventsBeforeAnyCommit() {
        BoundedDrainContext ctx = CreateActorForBoundedDrain(
            new EventDrainOptions { MaxOutstandingPublicationEntries = 1 });
        ConfigureFreshCommandState(ctx);
        _ = ctx.StateManager.TryGetStateAsync<UnpublishedPublicationIndex>(
            UnpublishedPublicationIndex.StateKey, Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<UnpublishedPublicationIndex>(
                true,
                new UnpublishedPublicationIndex([
                    new UnpublishedPublicationEntry("msg-other", "corr-other", DateTimeOffset.UtcNow),
                ])));

        var calls = new List<string>();
        ctx.StateManager
            .When(s => s.SetStateAsync(
                Arg.Is<string>(k => k.Contains(":events:", StringComparison.Ordinal)),
                Arg.Any<EventEnvelope>(),
                Arg.Any<CancellationToken>()))
            .Do(_ => calls.Add("stage-events"));
        ctx.StateManager.When(s => s.ClearCacheAsync(Arg.Any<CancellationToken>())).Do(_ => calls.Add("clear"));
        ctx.StateManager.When(s => s.SaveStateAsync(Arg.Any<CancellationToken>())).Do(_ => calls.Add("save"));

        CommandProcessingResult result = await ctx.Actor.ProcessCommandAsync(CreateCapacityEnvelope());

        result.Accepted.ShouldBeFalse();
        result.BackpressureExceeded.ShouldBeTrue();
        result.FailureReason.ShouldBe("BackpressureExceeded");
        result.BackpressureThreshold.ShouldBe(1);

        int stageIndex = calls.LastIndexOf("stage-events");
        stageIndex.ShouldBeGreaterThanOrEqualTo(0, "the persistence step must have staged events");
        int clearIndex = calls.IndexOf("clear", stageIndex);
        clearIndex.ShouldBeGreaterThan(stageIndex, "the staged events must be discarded");
        calls
            .Skip(stageIndex + 1)
            .Take(clearIndex - stageIndex - 1)
            .ShouldNotContain("save", "no commit may happen between staging the events and discarding them");
    }

    [Fact]
    public async Task ProcessCommand_PublicationIndexAtCapacity_NeitherPublishesNorCreatesADrainRecord() {
        BoundedDrainContext ctx = CreateActorForBoundedDrain(
            new EventDrainOptions { MaxOutstandingPublicationEntries = 1 });
        ConfigureFreshCommandState(ctx);
        _ = ctx.StateManager.TryGetStateAsync<UnpublishedPublicationIndex>(
            UnpublishedPublicationIndex.StateKey, Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<UnpublishedPublicationIndex>(
                true,
                new UnpublishedPublicationIndex([
                    new UnpublishedPublicationEntry("msg-other", "corr-other", DateTimeOffset.UtcNow),
                ])));

        _ = await ctx.Actor.ProcessCommandAsync(CreateCapacityEnvelope());

        _ = await ctx.EventPublisher.DidNotReceive().PublishEventsAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<bool>());
        await ctx.StateManager.DidNotReceive().SetStateAsync(
            Arg.Is<string>(s => s.StartsWith("drain:", StringComparison.Ordinal)),
            Arg.Any<UnpublishedEventsRecord>(),
            Arg.Any<CancellationToken>());
    }

    private static CommandEnvelope CreateCapacityEnvelope() => new(
        MessageId: "msg-capacity",
        TenantId: "test-tenant",
        Domain: "test-domain",
        AggregateId: "agg-001",
        CommandType: "CreateOrder",
        Payload: [1, 2, 3],
        CorrelationId: "corr-capacity",
        CausationId: null,
        UserId: "system",
        Extensions: null);

    private static void ConfigureFreshCommandState(BoundedDrainContext context) {
        _ = context.StateManager.TryGetStateAsync<IdempotencyRecord>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<IdempotencyRecord>(false, default!));
        _ = context.StateManager.TryGetStateAsync<AggregateMetadata>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<AggregateMetadata>(false, default!));
        _ = context.StateManager.TryGetStateAsync<PipelineState>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<PipelineState>(false, default!));
        _ = context.Invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>())
            .Returns(Hexalith.EventStore.Contracts.Results.DomainResult.Success([new CapacityTestEvent()]));
        _ = context.EventPublisher.PublishEventsAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<bool>())
            .Returns(callInfo => new EventPublishResult(true, callInfo.ArgAt<IReadOnlyList<EventEnvelope>>(1).Count, null));
    }

    private sealed record CapacityTestEvent : Hexalith.EventStore.Contracts.Events.IEventPayload;
}
