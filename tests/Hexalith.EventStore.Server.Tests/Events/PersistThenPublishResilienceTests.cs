
using Dapr.Actors;
using Dapr.Actors.Runtime;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.Tests.TestUtilities;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

using EventEnvelope = Hexalith.EventStore.Server.Events.EventEnvelope;

namespace Hexalith.EventStore.Server.Tests.Events;
/// <summary>
/// Story 4.2: Persist-then-publish resilience tests.
/// Verifies UnpublishedEventsRecord storage on PublishFailed paths (AC: #2, #3, #7, #8).
/// </summary>
public class PersistThenPublishResilienceTests {
    private static CommandEnvelope CreateTestEnvelope(
        string? correlationId = null,
        string? causationId = null,
        string messageId = "msg-resilience") => new(
        MessageId: messageId,
        TenantId: "test-tenant",
        Domain: "test-domain",
        AggregateId: "agg-001",
        CommandType: "CreateOrder",
        Payload: [1, 2, 3],
        CorrelationId: correlationId ?? "corr-resilience",
        CausationId: causationId,
        UserId: "system",
        Extensions: null);

    private static (AggregateActor Actor, IActorStateManager StateManager, IEventPublisher EventPublisher)
        CreateActor() {
        (AggregateActor actor, IActorStateManager stateManager, IEventPublisher eventPublisher, _) =
            CreateActorWithTimerManager();
        return (actor, stateManager, eventPublisher);
    }

    private static (AggregateActor Actor, IActorStateManager StateManager, IEventPublisher EventPublisher, ActorTimerManager TimerManager)
        CreateActorWithTimerManager(EventDrainOptions? drainOptions = null, int domainEventCount = 1) {
        IActorStateManager stateManager = Substitute.For<IActorStateManager>();
        ILogger<AggregateActor> logger = Substitute.For<ILogger<AggregateActor>>();
        IDomainServiceInvoker invoker = Substitute.For<IDomainServiceInvoker>();
        ISnapshotManager snapshotManager = Substitute.For<ISnapshotManager>();
        ICommandStatusStore statusStore = Substitute.For<ICommandStatusStore>();
        IEventPublisher eventPublisher = Substitute.For<IEventPublisher>();
        ActorTimerManager timerManager = Substitute.For<ActorTimerManager>();
        var host = ActorHost.CreateForTest<AggregateActor>(
            new ActorTestOptions { ActorId = new ActorId("test-tenant:test-domain:agg-001"), TimerManager = timerManager });
        var actor = new AggregateActor(host, logger, invoker, snapshotManager, new NoOpEventPayloadProtectionService(), statusStore, eventPublisher, Options.Create(drainOptions ?? new EventDrainOptions()), Options.Create(new BackpressureOptions()), Substitute.For<IDeadLetterPublisher>());

        ActorStateManagerTestHelper.SetStateManager(actor, stateManager);

        // Default: no idempotency record
        _ = stateManager.TryGetStateAsync<IdempotencyRecord>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<IdempotencyRecord>(false, default!));

        // Default: no metadata (new aggregate)
        _ = stateManager.TryGetStateAsync<AggregateMetadata>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<AggregateMetadata>(false, default!));

        // Default: no pipeline state
        _ = stateManager.TryGetStateAsync<PipelineState>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<PipelineState>(false, default!));

        // Default: domain returns success with event(s)
        TestEvent[] events = Enumerable.Range(0, domainEventCount)
            .Select(_ => new TestEvent())
            .ToArray();
        _ = invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>())
            .Returns(DomainResult.Success(events));

        return (actor, stateManager, eventPublisher, timerManager);
    }

    // --- Task 8.2: PublishFailed stores UnpublishedEventsRecord ---

    [Fact]
    public async Task ProcessCommand_PublishFailed_UnpublishedRecordStored() {
        // Arrange
        (AggregateActor actor, IActorStateManager stateManager, IEventPublisher eventPublisher) = CreateActor();
        CommandEnvelope envelope = CreateTestEnvelope();
        IdempotencyRecord? storedIdempotencyRecord = null;
        _ = stateManager.SetStateAsync(
            $"idempotency:{envelope.MessageId}",
            Arg.Do<IdempotencyRecord>(record => storedIdempotencyRecord = record),
            Arg.Any<CancellationToken>());

        _ = eventPublisher.PublishEventsAsync(
            Arg.Any<AggregateIdentity>(),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<bool>())
            .Returns(new EventPublishResult(false, 0, "Pub/sub unavailable"));

        // Act
        CommandProcessingResult result = await actor.ProcessCommandAsync(envelope);

        // Assert
        result.Accepted.ShouldBeTrue();
        await stateManager.Received(1).SetStateAsync(
            $"idempotency:{envelope.MessageId}",
            Arg.Is<IdempotencyRecord>(record =>
                record.Disposition == IdempotencyRecordDisposition.Recoverable
                && record.ExpiresAt.HasValue),
            Arg.Any<CancellationToken>());
        await stateManager.Received().SetStateAsync(
            Arg.Is<string>(s => s.StartsWith("drain:")),
            Arg.Is<UnpublishedEventsRecord>(r =>
                r.CorrelationId == envelope.CorrelationId &&
                r.EventCount == 1 &&
                r.RetryCount == 0 &&
                r.LastFailureReason == "Pub/sub unavailable"),
            Arg.Any<CancellationToken>());

        _ = stateManager.TryGetStateAsync<IdempotencyRecord>(
            $"idempotency:{envelope.MessageId}",
            Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<IdempotencyRecord>(true, storedIdempotencyRecord.ShouldNotBeNull()));

        CommandProcessingResult retry = await actor.ProcessCommandAsync(envelope);

        retry.ShouldBe(result);
        _ = await eventPublisher.Received(1).PublishEventsAsync(
            Arg.Any<AggregateIdentity>(),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<bool>());
    }

    // --- Task 8.3: Record contains correct sequence range ---

    [Fact]
    public async Task ProcessCommand_PublishFailed_RecordContainsCorrectSequenceRange() {
        // Arrange
        (AggregateActor actor, IActorStateManager stateManager, IEventPublisher eventPublisher) = CreateActor();
        CommandEnvelope envelope = CreateTestEnvelope();

        _ = eventPublisher.PublishEventsAsync(
            Arg.Any<AggregateIdentity>(),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<bool>())
            .Returns(new EventPublishResult(false, 0, "unavailable"));

        // Act
        _ = await actor.ProcessCommandAsync(envelope);

        // Assert -- new aggregate: first event at seq 1
        await stateManager.Received().SetStateAsync(
            Arg.Is<string>(s => s.StartsWith("drain:")),
            Arg.Is<UnpublishedEventsRecord>(r =>
                r.StartSequence == 1 &&
                r.EndSequence == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessCommand_PartialPublishFailed_RecordContainsFullPersistedSequenceRange() {
        // Arrange
        (AggregateActor actor, IActorStateManager stateManager, IEventPublisher eventPublisher, _) =
            CreateActorWithTimerManager(domainEventCount: 3);
        CommandEnvelope envelope = CreateTestEnvelope();

        _ = eventPublisher.PublishEventsAsync(
            Arg.Any<AggregateIdentity>(),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<bool>())
            .Returns(new EventPublishResult(false, 2, "Connection reset after event 2"));

        // Act
        CommandProcessingResult result = await actor.ProcessCommandAsync(envelope);

        // Assert
        result.Accepted.ShouldBeTrue();
        await stateManager.Received().SetStateAsync(
            Arg.Is<string>(s => s.StartsWith("drain:")),
            Arg.Is<UnpublishedEventsRecord>(r =>
                r.CorrelationId == envelope.CorrelationId
                && r.EventCount == 3
                && r.StartSequence == 1
                && r.EndSequence == 3
                && r.RetryCount == 0
                && r.LastFailureReason == "Connection reset after event 2"),
            Arg.Any<CancellationToken>());
    }

    // --- Task 8.4: Drain reminder registered after PublishFailed ---

    [Fact]
    public async Task ProcessCommand_PublishFailed_DrainReminderRegistered() {
        // Arrange
        (AggregateActor actor, IActorStateManager stateManager, IEventPublisher eventPublisher, ActorTimerManager timerManager) =
            CreateActorWithTimerManager();
        CommandEnvelope envelope = CreateTestEnvelope();

        _ = eventPublisher.PublishEventsAsync(
            Arg.Any<AggregateIdentity>(),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<bool>())
            .Returns(new EventPublishResult(false, 0, "Pub/sub unavailable"));

        // Act
        CommandProcessingResult result = await actor.ProcessCommandAsync(envelope);

        // Assert
        result.Accepted.ShouldBeTrue();
        await timerManager.Received().RegisterReminderAsync(
            Arg.Is<ActorReminder>(r =>
                r.Name == UnpublishedEventsRecord.GetReminderName(envelope.MessageId)));
    }

    // --- Story 4.2 Task 7.1: Drain reminder uses configured timing values ---

    [Fact]
    public async Task ProcessCommand_PublishFailed_DrainReminderUsesConfiguredTiming() {
        // Arrange -- create actor with custom EventDrainOptions
        var customOptions = new EventDrainOptions {
            InitialDrainDelay = TimeSpan.FromSeconds(45),
            DrainPeriod = TimeSpan.FromMinutes(2),
        };
        (AggregateActor actor, IActorStateManager stateManager, IEventPublisher eventPublisher, ActorTimerManager timerManager) =
            CreateActorWithTimerManager(customOptions);
        CommandEnvelope envelope = CreateTestEnvelope();

        _ = eventPublisher.PublishEventsAsync(
            Arg.Any<AggregateIdentity>(),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<bool>())
            .Returns(new EventPublishResult(false, 0, "Pub/sub unavailable"));

        // Act
        CommandProcessingResult result = await actor.ProcessCommandAsync(envelope);

        // Assert -- publish was attempted and failed (confirming drain path was triggered)
        result.Accepted.ShouldBeTrue();
        _ = await eventPublisher.Received(1).PublishEventsAsync(
            Arg.Any<AggregateIdentity>(),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<bool>());

        // Assert -- reminder name is correct
        await timerManager.Received(1).RegisterReminderAsync(
            Arg.Is<ActorReminder>(r =>
                r.Name == UnpublishedEventsRecord.GetReminderName(envelope.MessageId)));

        // Assert -- reminder dueTime matches configured InitialDrainDelay
        await timerManager.Received(1).RegisterReminderAsync(
            Arg.Is<ActorReminder>(r =>
                r.DueTime == TimeSpan.FromSeconds(45)));

        // Assert -- reminder period matches configured DrainPeriod
        await timerManager.Received(1).RegisterReminderAsync(
            Arg.Is<ActorReminder>(r =>
                r.Period == TimeSpan.FromMinutes(2)));
    }

    // --- Story 4.2 Task 7.1 Patch: MaxDrainPeriod clamping enforced ---

    [Fact]
    public async Task ProcessCommand_PublishFailed_DrainReminderClampsToMaxPeriod() {
        // Arrange -- create actor with DrainPeriod exceeding MaxDrainPeriod
        var customOptions = new EventDrainOptions {
            InitialDrainDelay = TimeSpan.FromSeconds(30),
            DrainPeriod = TimeSpan.FromMinutes(45),  // Exceeds default MaxDrainPeriod (30 min)
            MaxDrainPeriod = TimeSpan.FromMinutes(30),
        };
        (AggregateActor actor, IActorStateManager stateManager, IEventPublisher eventPublisher, ActorTimerManager timerManager) =
            CreateActorWithTimerManager(customOptions);
        CommandEnvelope envelope = CreateTestEnvelope();

        _ = eventPublisher.PublishEventsAsync(
            Arg.Any<AggregateIdentity>(),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<bool>())
            .Returns(new EventPublishResult(false, 0, "Pub/sub unavailable"));

        // Act
        CommandProcessingResult result = await actor.ProcessCommandAsync(envelope);

        // Assert -- reminder period should be clamped to MaxDrainPeriod (30 min), not 45 min
        result.Accepted.ShouldBeTrue();
        await timerManager.Received(1).RegisterReminderAsync(
            Arg.Is<ActorReminder>(r =>
                r.Period == TimeSpan.FromMinutes(30)));
    }

    // --- Story 4.2 Task 7.1 Patch: InitialDrainDelay boundary values ---

    [Theory]
    [InlineData(0)]      // TimeSpan.Zero — immediate drain
    [InlineData(-1)]     // Negative — should clamp to Zero
    public async Task ProcessCommand_PublishFailed_DrainReminderWithBoundaryInitialDelay(int delaySeconds) {
        // Arrange
        var customOptions = new EventDrainOptions {
            InitialDrainDelay = TimeSpan.FromSeconds(delaySeconds),
            DrainPeriod = TimeSpan.FromMinutes(1),
        };
        (AggregateActor actor, IActorStateManager stateManager, IEventPublisher eventPublisher, ActorTimerManager timerManager) =
            CreateActorWithTimerManager(customOptions);
        CommandEnvelope envelope = CreateTestEnvelope();

        _ = eventPublisher.PublishEventsAsync(
            Arg.Any<AggregateIdentity>(),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<bool>())
            .Returns(new EventPublishResult(false, 0, "Pub/sub unavailable"));

        // Act
        CommandProcessingResult result = await actor.ProcessCommandAsync(envelope);

        // Assert -- dueTime should be clamped to >= Zero
        TimeSpan expectedDueTime = delaySeconds < 0 ? TimeSpan.Zero : TimeSpan.FromSeconds(delaySeconds);
        result.Accepted.ShouldBeTrue();
        await timerManager.Received(1).RegisterReminderAsync(
            Arg.Is<ActorReminder>(r =>
                r.DueTime == expectedDueTime));
    }

    // --- Story 4.2 Task 7.1 Patch: DrainPeriod boundary values ---

    [Theory]
    [InlineData(0)]      // Zero — should fall back to 1 min
    [InlineData(-1)]     // Negative — should fall back to 1 min
    public async Task ProcessCommand_PublishFailed_DrainReminderWithBoundaryPeriod(int periodSeconds) {
        // Arrange
        var customOptions = new EventDrainOptions {
            InitialDrainDelay = TimeSpan.FromSeconds(30),
            DrainPeriod = TimeSpan.FromSeconds(periodSeconds),
        };
        (AggregateActor actor, IActorStateManager stateManager, IEventPublisher eventPublisher, ActorTimerManager timerManager) =
            CreateActorWithTimerManager(customOptions);
        CommandEnvelope envelope = CreateTestEnvelope();

        _ = eventPublisher.PublishEventsAsync(
            Arg.Any<AggregateIdentity>(),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<bool>())
            .Returns(new EventPublishResult(false, 0, "Pub/sub unavailable"));

        // Act
        CommandProcessingResult result = await actor.ProcessCommandAsync(envelope);

        // Assert -- period should fall back to 1 min if invalid
        TimeSpan expectedPeriod = periodSeconds <= 0 ? TimeSpan.FromMinutes(1) : TimeSpan.FromSeconds(periodSeconds);
        result.Accepted.ShouldBeTrue();
        await timerManager.Received(1).RegisterReminderAsync(
            Arg.Is<ActorReminder>(r =>
                r.Period == expectedPeriod));
    }

    // --- Task 8.5: Events still in state store after PublishFailed ---

    [Fact]
    public async Task ProcessCommand_PublishFailed_EventsStillInStateStore() {
        // Arrange
        (AggregateActor actor, IActorStateManager stateManager, IEventPublisher eventPublisher) = CreateActor();
        CommandEnvelope envelope = CreateTestEnvelope();

        _ = eventPublisher.PublishEventsAsync(
            Arg.Any<AggregateIdentity>(),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<bool>())
            .Returns(new EventPublishResult(false, 0, "unavailable"));

        // Act
        _ = await actor.ProcessCommandAsync(envelope);

        // Assert -- events were persisted (SetStateAsync for event key was called before publish)
        await stateManager.Received().SetStateAsync(
            Arg.Is<string>(s => s.Contains(":events:")),
            Arg.Any<EventEnvelope>(),
            Arg.Any<CancellationToken>());
    }

    // --- Task 8.6: Success path has no drain record ---

    [Fact]
    public async Task ProcessCommand_PublishSuccess_NoUnpublishedRecord() {
        // Arrange
        (AggregateActor actor, IActorStateManager stateManager, IEventPublisher eventPublisher) = CreateActor();
        CommandEnvelope envelope = CreateTestEnvelope();

        _ = eventPublisher.PublishEventsAsync(
            Arg.Any<AggregateIdentity>(),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<bool>())
            .Returns(callInfo => new EventPublishResult(true, callInfo.ArgAt<IReadOnlyList<EventEnvelope>>(1).Count, null));

        // Act
        CommandProcessingResult result = await actor.ProcessCommandAsync(envelope);

        // Assert
        result.Accepted.ShouldBeTrue();
        await stateManager.DidNotReceive().SetStateAsync(
            Arg.Is<string>(s => s.StartsWith("drain:")),
            Arg.Any<UnpublishedEventsRecord>(),
            Arg.Any<CancellationToken>());
    }

    // --- Task 8.7: Resume path also stores drain record ---

    [Fact]
    public async Task ResumeFromEventsStored_PublishFails_UnpublishedRecordStored() {
        // Arrange -- simulate crash recovery with EventsStored pipeline state
        (AggregateActor actor, IActorStateManager stateManager, IEventPublisher eventPublisher) = CreateActor();

        var existingPipeline = new PipelineState(
            "corr-resilience", CommandStatus.EventsStored, "CreateOrder",
            DateTimeOffset.UtcNow.AddSeconds(-5), EventCount: 2, RejectionEventType: null,
            MessageId: "msg-resilience", CausationId: "msg-resilience",
            StartSequence: 1, EndSequence: 2);

        _ = stateManager.TryGetStateAsync<PipelineState>(
            Arg.Is<string>(s => s.Contains(":pipeline:corr-resilience")),
            Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<PipelineState>(true, existingPipeline));

        // Set up metadata and events for resume
        var metadata = new AggregateMetadata(2, DateTimeOffset.UtcNow, null);
        _ = stateManager.TryGetStateAsync<AggregateMetadata>(
            "test-tenant:test-domain:agg-001:metadata", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<AggregateMetadata>(true, metadata));

        for (int i = 1; i <= 2; i++) {
            int seq = i;
            var evt = new EventEnvelope(
                "msg-1", "agg-001", "test-aggregate", "test-tenant", "test-domain", seq, 0, DateTimeOffset.UtcNow,
                "corr-resilience", $"cause-{seq}", "system", "1.0.0", "TestEvent", 1, "json", [1], null);
            _ = stateManager.TryGetStateAsync<EventEnvelope>(
                $"test-tenant:test-domain:agg-001:events:{seq}", Arg.Any<CancellationToken>())
                .Returns(new ConditionalValue<EventEnvelope>(true, evt));
        }

        _ = eventPublisher.PublishEventsAsync(
            Arg.Any<AggregateIdentity>(),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<bool>())
            .Returns(new EventPublishResult(false, 0, "Pub/sub unavailable"));

        CommandEnvelope envelope = CreateTestEnvelope();

        // Act
        CommandProcessingResult result = await actor.ProcessCommandAsync(envelope);

        // Assert
        result.Accepted.ShouldBeTrue();
        await stateManager.Received().SetStateAsync(
            Arg.Is<string>(s => s.StartsWith("drain:")),
            Arg.Is<UnpublishedEventsRecord>(r =>
                r.CorrelationId == "corr-resilience" &&
                r.EventCount == 2 &&
                r.StartSequence == 1 &&
                r.EndSequence == 2),
            Arg.Any<CancellationToken>());
    }

    // --- Task 8.8: Resume path drain reminder registered ---

    [Fact]
    public async Task ResumeFromEventsStored_PublishFails_DrainReminderRegistered() {
        // Arrange -- simulate crash recovery with EventsStored pipeline state
        (AggregateActor actor, IActorStateManager stateManager, IEventPublisher eventPublisher, ActorTimerManager timerManager) =
            CreateActorWithTimerManager();

        var existingPipeline = new PipelineState(
            "corr-resilience", CommandStatus.EventsStored, "CreateOrder",
            DateTimeOffset.UtcNow.AddSeconds(-5), EventCount: 2, RejectionEventType: null,
            MessageId: "msg-resilience", CausationId: "msg-resilience",
            StartSequence: 1, EndSequence: 2);

        _ = stateManager.TryGetStateAsync<PipelineState>(
            Arg.Is<string>(s => s.Contains(":pipeline:corr-resilience")),
            Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<PipelineState>(true, existingPipeline));

        // Set up metadata and events for resume
        var metadata = new AggregateMetadata(2, DateTimeOffset.UtcNow, null);
        _ = stateManager.TryGetStateAsync<AggregateMetadata>(
            "test-tenant:test-domain:agg-001:metadata", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<AggregateMetadata>(true, metadata));

        for (int i = 1; i <= 2; i++) {
            int seq = i;
            var evt = new EventEnvelope(
                "msg-1", "agg-001", "test-aggregate", "test-tenant", "test-domain", seq, 0, DateTimeOffset.UtcNow,
                "corr-resilience", $"cause-{seq}", "system", "1.0.0", "TestEvent", 1, "json", [1], null);
            _ = stateManager.TryGetStateAsync<EventEnvelope>(
                $"test-tenant:test-domain:agg-001:events:{seq}", Arg.Any<CancellationToken>())
                .Returns(new ConditionalValue<EventEnvelope>(true, evt));
        }

        _ = eventPublisher.PublishEventsAsync(
            Arg.Any<AggregateIdentity>(),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<bool>())
            .Returns(new EventPublishResult(false, 0, "Pub/sub unavailable"));

        CommandEnvelope envelope = CreateTestEnvelope();

        // Act
        CommandProcessingResult result = await actor.ProcessCommandAsync(envelope);

        // Assert
        result.Accepted.ShouldBeTrue();
        await timerManager.Received().RegisterReminderAsync(
            Arg.Is<ActorReminder>(r =>
                r.Name == UnpublishedEventsRecord.GetReminderName("msg-resilience")));
    }

    [Fact]
    public async Task ResumeFromEventsStored_PublishFails_MetadataMissingAfterLoad_StillStoresRecordFromLoadedEvents() {
        // Arrange -- metadata is available for initial load, but unavailable afterward.
        (AggregateActor actor, IActorStateManager stateManager, IEventPublisher eventPublisher) = CreateActor();

        var existingPipeline = new PipelineState(
            "corr-resilience", CommandStatus.EventsStored, "CreateOrder",
            DateTimeOffset.UtcNow.AddSeconds(-5), EventCount: 2, RejectionEventType: null,
            MessageId: "msg-resilience", CausationId: "msg-resilience",
            StartSequence: 1, EndSequence: 2);

        _ = stateManager.TryGetStateAsync<PipelineState>(
            Arg.Is<string>(s => s.Contains(":pipeline:corr-resilience")),
            Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<PipelineState>(true, existingPipeline));

        var metadata = new AggregateMetadata(2, DateTimeOffset.UtcNow, null);
        _ = stateManager.TryGetStateAsync<AggregateMetadata>(
            "test-tenant:test-domain:agg-001:metadata", Arg.Any<CancellationToken>())
            .Returns(
                new ConditionalValue<AggregateMetadata>(true, metadata),
                new ConditionalValue<AggregateMetadata>(false, default!));

        for (int i = 1; i <= 2; i++) {
            int seq = i;
            var evt = new EventEnvelope(
                "msg-1", "agg-001", "test-aggregate", "test-tenant", "test-domain", seq, 0, DateTimeOffset.UtcNow,
                "corr-resilience", $"cause-{seq}", "system", "1.0.0", "TestEvent", 1, "json", [1], null);
            _ = stateManager.TryGetStateAsync<EventEnvelope>(
                $"test-tenant:test-domain:agg-001:events:{seq}", Arg.Any<CancellationToken>())
                .Returns(new ConditionalValue<EventEnvelope>(true, evt));
        }

        _ = eventPublisher.PublishEventsAsync(
            Arg.Any<AggregateIdentity>(),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<bool>())
            .Returns(new EventPublishResult(false, 0, "Pub/sub unavailable"));

        CommandEnvelope envelope = CreateTestEnvelope();

        // Act
        CommandProcessingResult result = await actor.ProcessCommandAsync(envelope);

        // Assert
        result.Accepted.ShouldBeTrue();
        await stateManager.Received().SetStateAsync(
            Arg.Is<string>(s => s.StartsWith("drain:")),
            Arg.Is<UnpublishedEventsRecord>(r =>
                r.CorrelationId == "corr-resilience"
                && r.StartSequence == 1
                && r.EndSequence == 2
                && r.EventCount == 2),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResumeFromEventsStored_LegacyCheckpointWithoutRange_FailsClosedWithoutPublishing() {
        // Arrange -- a legacy committed checkpoint without a persisted StartSequence/EndSequence cannot
        // have its events identified safely (an interleaved command may have advanced the stream head),
        // so resume must fail closed rather than re-publish a guessed range.
        (AggregateActor actor, IActorStateManager stateManager, IEventPublisher eventPublisher) = CreateActor();

        var existingPipeline = new PipelineState(
            "corr-resilience", CommandStatus.EventsStored, "CreateOrder",
            DateTimeOffset.UtcNow.AddSeconds(-5), EventCount: 2, RejectionEventType: null,
            MessageId: "msg-resilience", CausationId: "msg-resilience");

        _ = stateManager.TryGetStateAsync<PipelineState>(
            Arg.Is<string>(s => s.Contains(":pipeline:corr-resilience")),
            Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<PipelineState>(true, existingPipeline));

        CommandEnvelope envelope = CreateTestEnvelope();

        // Act
        CommandProcessingResult result = await actor.ProcessCommandAsync(envelope);

        // Assert -- fail closed with the identity-conflict contract; the checkpoint is preserved and
        // nothing is published under a re-derived range.
        result.Accepted.ShouldBeFalse();
        result.ErrorMessage.ShouldBe("command_identity_conflict");

        _ = await eventPublisher.DidNotReceive().PublishEventsAsync(
            Arg.Any<AggregateIdentity>(),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<bool>());
    }

    private sealed record TestEvent : IEventPayload;
}
