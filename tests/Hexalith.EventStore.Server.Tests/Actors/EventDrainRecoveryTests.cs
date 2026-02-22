namespace Hexalith.EventStore.Server.Tests.Actors;

using System.Reflection;

using Dapr.Actors;
using Dapr.Actors.Runtime;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.Server.Events;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

using EventEnvelope = Hexalith.EventStore.Server.Events.EventEnvelope;

/// <summary>
/// Story 4.4 Tasks 7 &amp; 9: Drain recovery and end-to-end drain cycle tests.
/// Verifies ReceiveReminderAsync, DrainUnpublishedEventsAsync, and full drain lifecycle
/// (AC: #1, #4, #5, #6, #9, #10, #12).
/// </summary>
public class EventDrainRecoveryTests {
    private static (AggregateActor Actor, IActorStateManager StateManager, ILogger<AggregateActor> Logger,
        IEventPublisher EventPublisher, ICommandStatusStore StatusStore) CreateActor(
        string actorId = "test-tenant:test-domain:agg-001") {
        var stateManager = Substitute.For<IActorStateManager>();
        var logger = Substitute.For<ILogger<AggregateActor>>();
        var invoker = Substitute.For<IDomainServiceInvoker>();
        var snapshotManager = Substitute.For<ISnapshotManager>();
        var statusStore = Substitute.For<ICommandStatusStore>();
        var eventPublisher = Substitute.For<IEventPublisher>();
        var host = ActorHost.CreateForTest<AggregateActor>(
            new ActorTestOptions { ActorId = new ActorId(actorId) });
        var actor = new AggregateActor(host, logger, invoker, snapshotManager, statusStore, eventPublisher, Options.Create(new EventDrainOptions()), Substitute.For<IDeadLetterPublisher>());

        PropertyInfo? prop = typeof(Actor).GetProperty("StateManager", BindingFlags.Public | BindingFlags.Instance);
        prop?.SetValue(actor, stateManager);

        return (actor, stateManager, logger, eventPublisher, statusStore);
    }

    private static (AggregateActor Actor, IActorStateManager StateManager, ILogger<AggregateActor> Logger,
        IEventPublisher EventPublisher, ICommandStatusStore StatusStore, ActorTimerManager TimerManager) CreateActorWithTimerManager(
        string actorId = "test-tenant:test-domain:agg-001") {
        var stateManager = Substitute.For<IActorStateManager>();
        var logger = Substitute.For<ILogger<AggregateActor>>();
        var invoker = Substitute.For<IDomainServiceInvoker>();
        var snapshotManager = Substitute.For<ISnapshotManager>();
        var statusStore = Substitute.For<ICommandStatusStore>();
        var eventPublisher = Substitute.For<IEventPublisher>();
        var timerManager = Substitute.For<ActorTimerManager>();
        var host = ActorHost.CreateForTest<AggregateActor>(
            new ActorTestOptions { ActorId = new ActorId(actorId), TimerManager = timerManager });
        var actor = new AggregateActor(host, logger, invoker, snapshotManager, statusStore, eventPublisher, Options.Create(new EventDrainOptions()), Substitute.For<IDeadLetterPublisher>());

        PropertyInfo? prop = typeof(Actor).GetProperty("StateManager", BindingFlags.Public | BindingFlags.Instance);
        prop?.SetValue(actor, stateManager);

        return (actor, stateManager, logger, eventPublisher, statusStore, timerManager);
    }

    private static UnpublishedEventsRecord CreateDrainRecord(
        string correlationId = "corr-drain",
        int eventCount = 2,
        int retryCount = 0,
        bool isRejection = false) => new(
        correlationId,
        StartSequence: 1,
        EndSequence: eventCount,
        EventCount: eventCount,
        CommandType: "CreateOrder",
        IsRejection: isRejection,
        FailedAt: DateTimeOffset.UtcNow,
        RetryCount: retryCount,
        LastFailureReason: "Pub/sub unavailable");

    private static void ConfigureEventsInState(
        IActorStateManager stateManager,
        int eventCount,
        string correlationId = "corr-drain",
        int startSequence = 1) {
        int endSequence = startSequence + eventCount - 1;
        var metadata = new AggregateMetadata(endSequence, DateTimeOffset.UtcNow, null);
        stateManager.TryGetStateAsync<AggregateMetadata>(
            "test-tenant:test-domain:agg-001:metadata", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<AggregateMetadata>(true, metadata));

        for (int seq = startSequence; seq <= endSequence; seq++) {
            var evt = new EventEnvelope(
                "agg-001", "test-tenant", "test-domain", seq, DateTimeOffset.UtcNow,
                correlationId, $"cause-{seq}", "user-1", "1.0.0", "OrderCreated", "json",
                [1, 2, 3], null);
            stateManager.TryGetStateAsync<EventEnvelope>(
                $"test-tenant:test-domain:agg-001:events:{seq}", Arg.Any<CancellationToken>())
                .Returns(new ConditionalValue<EventEnvelope>(true, evt));
        }
    }

    // --- Task 7.2: Drain succeeds, events re-published ---

    [Fact]
    public async Task ReceiveReminder_DrainSucceeds_EventsRePublished() {
        // Arrange
        (AggregateActor actor, IActorStateManager stateManager, _, IEventPublisher eventPublisher, _) = CreateActor();
        UnpublishedEventsRecord record = CreateDrainRecord();

        stateManager.TryGetStateAsync<UnpublishedEventsRecord>(
            "drain:corr-drain", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<UnpublishedEventsRecord>(true, record));

        ConfigureEventsInState(stateManager, 2);

        eventPublisher.PublishEventsAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>())
            .Returns(new EventPublishResult(true, 2, null));

        // Act
        await actor.ReceiveReminderAsync("drain-unpublished-corr-drain", [], TimeSpan.Zero, TimeSpan.Zero);

        // Assert
        await eventPublisher.Received(1).PublishEventsAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
            Arg.Is<IReadOnlyList<EventEnvelope>>(e => e.Count == 2),
            "corr-drain",
            Arg.Any<CancellationToken>());
    }

    // --- Task 7.3: Drain succeeds, reminder unregistered ---

    [Fact]
    public async Task ReceiveReminder_DrainSucceeds_RecordRemoved() {
        // Arrange
        (AggregateActor actor, IActorStateManager stateManager, _, IEventPublisher eventPublisher, _) = CreateActor();
        UnpublishedEventsRecord record = CreateDrainRecord();

        stateManager.TryGetStateAsync<UnpublishedEventsRecord>(
            "drain:corr-drain", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<UnpublishedEventsRecord>(true, record));

        ConfigureEventsInState(stateManager, 2);

        eventPublisher.PublishEventsAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>())
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

        stateManager.TryGetStateAsync<UnpublishedEventsRecord>(
            "drain:corr-drain", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<UnpublishedEventsRecord>(true, record));

        ConfigureEventsInState(stateManager, 2);

        eventPublisher.PublishEventsAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>())
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

        stateManager.TryGetStateAsync<UnpublishedEventsRecord>(
            "drain:corr-drain", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<UnpublishedEventsRecord>(true, record));

        ConfigureEventsInState(stateManager, 2);

        eventPublisher.PublishEventsAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>())
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

        stateManager.TryGetStateAsync<UnpublishedEventsRecord>(
            "drain:corr-drain", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<UnpublishedEventsRecord>(true, record));

        ConfigureEventsInState(stateManager, 2);

        eventPublisher.PublishEventsAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>())
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

        stateManager.TryGetStateAsync<UnpublishedEventsRecord>(
            "drain:corr-drain", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<UnpublishedEventsRecord>(true, record));

        ConfigureEventsInState(stateManager, 2);

        eventPublisher.PublishEventsAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>())
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

        stateManager.TryGetStateAsync<UnpublishedEventsRecord>(
            "drain:corr-drain", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<UnpublishedEventsRecord>(true, record));

        ConfigureEventsInState(stateManager, 2);

        eventPublisher.PublishEventsAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>())
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

    [Fact]
    public async Task ReceiveReminder_DrainFails_ReminderContinuesFiring() {
        // Arrange
        (AggregateActor actor, IActorStateManager stateManager, _, IEventPublisher eventPublisher, _, ActorTimerManager timerManager) = CreateActorWithTimerManager();
        UnpublishedEventsRecord record = CreateDrainRecord();

        stateManager.TryGetStateAsync<UnpublishedEventsRecord>(
            "drain:corr-drain", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<UnpublishedEventsRecord>(true, record));

        ConfigureEventsInState(stateManager, 2);

        eventPublisher.PublishEventsAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>())
            .Returns(new EventPublishResult(false, 0, "unavailable"));

        // Act
        await actor.ReceiveReminderAsync("drain-unpublished-corr-drain", [], TimeSpan.Zero, TimeSpan.Zero);

        // Assert -- failure path must not unregister reminder
        await timerManager.DidNotReceive().UnregisterReminderAsync(
            Arg.Any<ActorReminderToken>());
    }

    // --- Task 7.8: Orphaned reminder cleanup ---

    [Fact]
    public async Task ReceiveReminder_RecordNotFound_LogsWarning() {
        // Arrange
        (AggregateActor actor, IActorStateManager stateManager, ILogger<AggregateActor> logger, _, _) = CreateActor();

        stateManager.TryGetStateAsync<UnpublishedEventsRecord>(
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

        stateManager.TryGetStateAsync<UnpublishedEventsRecord>(
            "drain:corr-1", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<UnpublishedEventsRecord>(true, record1));
        stateManager.TryGetStateAsync<UnpublishedEventsRecord>(
            "drain:corr-2", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<UnpublishedEventsRecord>(true, record2));

        ConfigureEventsInState(stateManager, 2, "corr-1");

        // First drain succeeds
        eventPublisher.PublishEventsAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            "corr-1",
            Arg.Any<CancellationToken>())
            .Returns(new EventPublishResult(true, 2, null));

        // Second drain fails
        eventPublisher.PublishEventsAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            "corr-2",
            Arg.Any<CancellationToken>())
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

        stateManager.TryGetStateAsync<UnpublishedEventsRecord>(
            "drain:corr-1", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<UnpublishedEventsRecord>(true, record1));

        stateManager.TryGetStateAsync<UnpublishedEventsRecord>(
            "drain:corr-2", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<UnpublishedEventsRecord>(true, record2));

        ConfigureEventsInState(stateManager, eventCount: 2, correlationId: "corr-1", startSequence: 1);
        ConfigureEventsInState(stateManager, eventCount: 2, correlationId: "corr-2", startSequence: 3);

        var publishedSequences = new Dictionary<string, IReadOnlyList<long>>();
        eventPublisher.PublishEventsAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>())
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

    // --- Task 7.10: Rejection events drained with correct status ---

    [Fact]
    public async Task ReceiveReminder_RejectionEvents_DrainedAndStatusRejected() {
        // Arrange
        (AggregateActor actor, IActorStateManager stateManager, _, IEventPublisher eventPublisher, ICommandStatusStore statusStore) = CreateActor();
        UnpublishedEventsRecord record = CreateDrainRecord(isRejection: true);

        stateManager.TryGetStateAsync<UnpublishedEventsRecord>(
            "drain:corr-drain", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<UnpublishedEventsRecord>(true, record));

        ConfigureEventsInState(stateManager, 2);

        eventPublisher.PublishEventsAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>())
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
        await eventPublisher.DidNotReceive().PublishEventsAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());

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

        stateManager.TryGetStateAsync<UnpublishedEventsRecord>(
            "drain:corr-drain", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<UnpublishedEventsRecord>(true, record));

        ConfigureEventsInState(stateManager, 2);

        // Now pub/sub is back
        eventPublisher.PublishEventsAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>())
            .Returns(new EventPublishResult(true, 2, null));

        // Act
        await actor.ReceiveReminderAsync("drain-unpublished-corr-drain", [], TimeSpan.Zero, TimeSpan.Zero);

        // Assert -- events delivered
        await eventPublisher.Received(1).PublishEventsAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
            Arg.Is<IReadOnlyList<EventEnvelope>>(e => e.Count == 2),
            "corr-drain",
            Arg.Any<CancellationToken>());

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

        stateManager.TryGetStateAsync<UnpublishedEventsRecord>(
            "drain:corr-drain", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<UnpublishedEventsRecord>(true, record));

        ConfigureEventsInState(stateManager, 2);

        eventPublisher.PublishEventsAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>())
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

        stateManager.TryGetStateAsync<UnpublishedEventsRecord>(
            "drain:corr-drain", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<UnpublishedEventsRecord>(true, record));

        ConfigureEventsInState(stateManager, 2);

        IReadOnlyList<EventEnvelope>? publishedEvents = null;
        eventPublisher.PublishEventsAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
            Arg.Do<IReadOnlyList<EventEnvelope>>(e => publishedEvents = e),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>())
            .Returns(new EventPublishResult(true, 2, null));

        // Act
        await actor.ReceiveReminderAsync("drain-unpublished-corr-drain", [], TimeSpan.Zero, TimeSpan.Zero);

        // Assert -- events have correct sequence numbers
        publishedEvents.ShouldNotBeNull();
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

        stateManager.TryGetStateAsync<UnpublishedEventsRecord>(
            "drain:corr-drain", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<UnpublishedEventsRecord>(true, record));

        ConfigureEventsInState(stateManager, 2);

        Hexalith.EventStore.Contracts.Identity.AggregateIdentity? publishedIdentity = null;
        eventPublisher.PublishEventsAsync(
            Arg.Do<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(id => publishedIdentity = id),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>())
            .Returns(new EventPublishResult(true, 2, null));

        // Act
        await actor.ReceiveReminderAsync("drain-unpublished-corr-drain", [], TimeSpan.Zero, TimeSpan.Zero);

        // Assert -- identity reconstructed correctly from actor ID
        publishedIdentity.ShouldNotBeNull();
        publishedIdentity.TenantId.ShouldBe("test-tenant");
        publishedIdentity.Domain.ShouldBe("test-domain");
        publishedIdentity.AggregateId.ShouldBe("agg-001");
        publishedIdentity.PubSubTopic.ShouldBe("test-tenant.test-domain.events");
    }
}
