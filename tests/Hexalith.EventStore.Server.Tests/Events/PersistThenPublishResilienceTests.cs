
using System.Reflection;

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

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

using EventEnvelope = Hexalith.EventStore.Server.Events.EventEnvelope;

namespace Hexalith.EventStore.Server.Tests.Events;
/// <summary>
/// Story 4.4 Task 8: Persist-then-publish resilience tests.
/// Verifies UnpublishedEventsRecord storage on PublishFailed paths (AC: #2, #3, #7, #8).
/// </summary>
public class PersistThenPublishResilienceTests {
    private static CommandEnvelope CreateTestEnvelope(
        string? correlationId = null,
        string? causationId = null) => new(
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
        CreateActorWithTimerManager() {
        IActorStateManager stateManager = Substitute.For<IActorStateManager>();
        ILogger<AggregateActor> logger = Substitute.For<ILogger<AggregateActor>>();
        IDomainServiceInvoker invoker = Substitute.For<IDomainServiceInvoker>();
        ISnapshotManager snapshotManager = Substitute.For<ISnapshotManager>();
        ICommandStatusStore statusStore = Substitute.For<ICommandStatusStore>();
        IEventPublisher eventPublisher = Substitute.For<IEventPublisher>();
        ActorTimerManager timerManager = Substitute.For<ActorTimerManager>();
        var host = ActorHost.CreateForTest<AggregateActor>(
            new ActorTestOptions { ActorId = new ActorId("test-tenant:test-domain:agg-001"), TimerManager = timerManager });
        var actor = new AggregateActor(host, logger, invoker, snapshotManager, new NoOpEventPayloadProtectionService(), statusStore, eventPublisher, Options.Create(new EventDrainOptions()), Substitute.For<IDeadLetterPublisher>());

        PropertyInfo? prop = typeof(Actor).GetProperty("StateManager", BindingFlags.Public | BindingFlags.Instance);
        prop?.SetValue(actor, stateManager);

        // Default: no idempotency record
        _ = stateManager.TryGetStateAsync<IdempotencyRecord>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<IdempotencyRecord>(false, default!));

        // Default: no metadata (new aggregate)
        _ = stateManager.TryGetStateAsync<AggregateMetadata>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<AggregateMetadata>(false, default!));

        // Default: no pipeline state
        _ = stateManager.TryGetStateAsync<PipelineState>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<PipelineState>(false, default!));

        // Default: domain returns success with event
        _ = invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>())
            .Returns(DomainResult.Success([new TestEvent()]));

        return (actor, stateManager, eventPublisher, timerManager);
    }

    // --- Task 8.2: PublishFailed stores UnpublishedEventsRecord ---

    [Fact]
    public async Task ProcessCommand_PublishFailed_UnpublishedRecordStored() {
        // Arrange
        (AggregateActor actor, IActorStateManager stateManager, IEventPublisher eventPublisher) = CreateActor();
        CommandEnvelope envelope = CreateTestEnvelope();

        _ = eventPublisher.PublishEventsAsync(
            Arg.Any<AggregateIdentity>(),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>())
            .Returns(new EventPublishResult(false, 0, "Pub/sub unavailable"));

        // Act
        CommandProcessingResult result = await actor.ProcessCommandAsync(envelope);

        // Assert
        result.Accepted.ShouldBeFalse();
        await stateManager.Received().SetStateAsync(
            Arg.Is<string>(s => s.StartsWith("drain:")),
            Arg.Is<UnpublishedEventsRecord>(r =>
                r.CorrelationId == envelope.CorrelationId &&
                r.EventCount == 1 &&
                r.RetryCount == 0 &&
                r.LastFailureReason == "Pub/sub unavailable"),
            Arg.Any<CancellationToken>());
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
            Arg.Any<CancellationToken>())
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
            Arg.Any<CancellationToken>())
            .Returns(new EventPublishResult(false, 0, "Pub/sub unavailable"));

        // Act
        CommandProcessingResult result = await actor.ProcessCommandAsync(envelope);

        // Assert
        result.Accepted.ShouldBeFalse();
        await timerManager.Received().RegisterReminderAsync(
            Arg.Is<ActorReminder>(r =>
                r.Name == UnpublishedEventsRecord.GetReminderName(envelope.CorrelationId)));
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
            Arg.Any<CancellationToken>())
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
            Arg.Any<CancellationToken>())
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
            DateTimeOffset.UtcNow.AddSeconds(-5), EventCount: 2, RejectionEventType: null);

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
                "agg-001", "test-tenant", "test-domain", seq, DateTimeOffset.UtcNow,
                "corr-resilience", $"cause-{seq}", "system", "1.0.0", "TestEvent", "json", [1], null);
            _ = stateManager.TryGetStateAsync<EventEnvelope>(
                $"test-tenant:test-domain:agg-001:events:{seq}", Arg.Any<CancellationToken>())
                .Returns(new ConditionalValue<EventEnvelope>(true, evt));
        }

        _ = eventPublisher.PublishEventsAsync(
            Arg.Any<AggregateIdentity>(),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>())
            .Returns(new EventPublishResult(false, 0, "Pub/sub unavailable"));

        CommandEnvelope envelope = CreateTestEnvelope();

        // Act
        CommandProcessingResult result = await actor.ProcessCommandAsync(envelope);

        // Assert
        result.Accepted.ShouldBeFalse();
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
            DateTimeOffset.UtcNow.AddSeconds(-5), EventCount: 2, RejectionEventType: null);

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
                "agg-001", "test-tenant", "test-domain", seq, DateTimeOffset.UtcNow,
                "corr-resilience", $"cause-{seq}", "system", "1.0.0", "TestEvent", "json", [1], null);
            _ = stateManager.TryGetStateAsync<EventEnvelope>(
                $"test-tenant:test-domain:agg-001:events:{seq}", Arg.Any<CancellationToken>())
                .Returns(new ConditionalValue<EventEnvelope>(true, evt));
        }

        _ = eventPublisher.PublishEventsAsync(
            Arg.Any<AggregateIdentity>(),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>())
            .Returns(new EventPublishResult(false, 0, "Pub/sub unavailable"));

        CommandEnvelope envelope = CreateTestEnvelope();

        // Act
        CommandProcessingResult result = await actor.ProcessCommandAsync(envelope);

        // Assert
        result.Accepted.ShouldBeFalse();
        await timerManager.Received().RegisterReminderAsync(
            Arg.Is<ActorReminder>(r =>
                r.Name == UnpublishedEventsRecord.GetReminderName("corr-resilience")));
    }

    [Fact]
    public async Task ResumeFromEventsStored_PublishFails_MetadataMissingAfterLoad_StillStoresRecordFromLoadedEvents() {
        // Arrange -- metadata is available for initial load, but unavailable afterward.
        (AggregateActor actor, IActorStateManager stateManager, IEventPublisher eventPublisher) = CreateActor();

        var existingPipeline = new PipelineState(
            "corr-resilience", CommandStatus.EventsStored, "CreateOrder",
            DateTimeOffset.UtcNow.AddSeconds(-5), EventCount: 2, RejectionEventType: null);

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
                "agg-001", "test-tenant", "test-domain", seq, DateTimeOffset.UtcNow,
                "corr-resilience", $"cause-{seq}", "system", "1.0.0", "TestEvent", "json", [1], null);
            _ = stateManager.TryGetStateAsync<EventEnvelope>(
                $"test-tenant:test-domain:agg-001:events:{seq}", Arg.Any<CancellationToken>())
                .Returns(new ConditionalValue<EventEnvelope>(true, evt));
        }

        _ = eventPublisher.PublishEventsAsync(
            Arg.Any<AggregateIdentity>(),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>())
            .Returns(new EventPublishResult(false, 0, "Pub/sub unavailable"));

        CommandEnvelope envelope = CreateTestEnvelope();

        // Act
        CommandProcessingResult result = await actor.ProcessCommandAsync(envelope);

        // Assert
        result.Accepted.ShouldBeFalse();
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
    public async Task ResumeFromEventsStored_PrepareFails_MetadataUnavailable_ThrowsInsteadOfSilentPublishFailed() {
        // Arrange -- resume starts at EventsStored, but metadata is unavailable so range cannot be computed for drain.
        (AggregateActor actor, IActorStateManager stateManager, IEventPublisher eventPublisher) = CreateActor();

        var existingPipeline = new PipelineState(
            "corr-resilience", CommandStatus.EventsStored, "CreateOrder",
            DateTimeOffset.UtcNow.AddSeconds(-5), EventCount: 2, RejectionEventType: null);

        _ = stateManager.TryGetStateAsync<PipelineState>(
            Arg.Is<string>(s => s.Contains(":pipeline:corr-resilience")),
            Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<PipelineState>(true, existingPipeline));

        _ = stateManager.TryGetStateAsync<AggregateMetadata>(
            "test-tenant:test-domain:agg-001:metadata", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<AggregateMetadata>(false, default!));

        CommandEnvelope envelope = CreateTestEnvelope();

        // Act + Assert
        _ = await Should.ThrowAsync<InvalidOperationException>(() => actor.ProcessCommandAsync(envelope));

        _ = await eventPublisher.DidNotReceive().PublishEventsAsync(
            Arg.Any<AggregateIdentity>(),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    private sealed record TestEvent : IEventPayload;
}
