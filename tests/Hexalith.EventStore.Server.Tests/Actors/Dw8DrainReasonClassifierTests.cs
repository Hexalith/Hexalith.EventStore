using System.Diagnostics;
using System.Net;

using Dapr;
using Dapr.Actors;
using Dapr.Actors.Runtime;

using Grpc.Core;

using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.Telemetry;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

using EventEnvelope = Hexalith.EventStore.Server.Events.EventEnvelope;
using Hexalith.EventStore.Server.Tests.TestUtilities;

namespace Hexalith.EventStore.Server.Tests.Actors;

public class Dw8DrainReasonClassifierTests {
    [Fact]
    public void DrainReasonCodes_StableWireValuesRemainLiteral() {
        DrainReasonCodes.EventCountMismatch.ShouldBe("drain_event_count_mismatch");
        DrainReasonCodes.MissingEvent.ShouldBe("drain_missing_event");
        DrainReasonCodes.PublishFailed.ShouldBe("drain_publish_failed");
        DrainReasonCodes.StateStoreFailure.ShouldBe("drain_state_store_failure");
        DrainReasonCodes.DaprUnavailable.ShouldBe("drain_dapr_unavailable");
        DrainReasonCodes.Unknown.ShouldBe("unknown");
    }

    [Fact]
    public void ClassifyDrainFailure_DaprUnavailableUsesStableCode() {
        var exception = new DaprException(
            "sidecar unavailable",
            new RpcException(new Status(StatusCode.Unavailable, "connection refused")));

        AggregateActor.ClassifyDrainFailure(exception).ShouldBe(DrainReasonCodes.DaprUnavailable);
    }

    [Fact]
    public void ClassifyDrainFailure_StateStoreBoundaryUsesStableCode() {
        var exception = new DrainStateStoreException(
            "Failed to load drain state.",
            new HttpRequestException("state store rejected write", null, HttpStatusCode.Conflict));

        AggregateActor.ClassifyDrainFailure(exception).ShouldBe(DrainReasonCodes.StateStoreFailure);
    }

    [Fact]
    public void ClassifyDrainFailure_ResidualExceptionKeepsUnknownWireValue() {
        AggregateActor.ClassifyDrainFailure(new InvalidOperationException("uncategorized"))
            .ShouldBe(DrainReasonCodes.Unknown);
    }

    [Fact]
    public async Task ReceiveReminder_DrainEventReadStateStoreFailure_ActivityFailureReasonIsStableCode() {
        (AggregateActor actor, IActorStateManager stateManager, _, _, _, _) = CreateActorWithTimerManager();
        const string correlationId = "corr-dw8-state";
        var record = new UnpublishedEventsRecord(
            CorrelationId: correlationId,
            StartSequence: 1,
            EndSequence: 1,
            EventCount: 1,
            CommandType: "CreateOrder",
            IsRejection: false,
            FailedAt: DateTimeOffset.UtcNow,
            RetryCount: 0,
            LastFailureReason: "Pub/sub unavailable");
        _ = stateManager.TryGetStateAsync<UnpublishedEventsRecord>(
            $"drain:{correlationId}", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<UnpublishedEventsRecord>(true, record));
        _ = stateManager.TryGetStateAsync<EventEnvelope>(
            "test-tenant:test-domain:agg-001:events:1", Arg.Any<CancellationToken>())
            .Returns(Task.FromException<ConditionalValue<EventEnvelope>>(new DaprException("state store unavailable")));

        Activity activity = await CaptureDrainActivityAsync(
            correlationId,
            () => actor.ReceiveReminderAsync($"drain-unpublished-{correlationId}", [], TimeSpan.Zero, TimeSpan.Zero));

        activity.GetTagItem("eventstore.failure_reason").ShouldBe(DrainReasonCodes.StateStoreFailure);
    }

    [Fact]
    public async Task ReceiveReminder_DrainPublishThrows_ActivityFailureReasonIsPublishFailed() {
        (AggregateActor actor, IActorStateManager stateManager, _, IEventPublisher eventPublisher, _) = CreateActor();
        const string correlationId = "corr-dw8-publish";
        var record = new UnpublishedEventsRecord(
            CorrelationId: correlationId,
            StartSequence: 1,
            EndSequence: 1,
            EventCount: 1,
            CommandType: "CreateOrder",
            IsRejection: false,
            FailedAt: DateTimeOffset.UtcNow,
            RetryCount: 0,
            LastFailureReason: "Pub/sub unavailable");
        _ = stateManager.TryGetStateAsync<UnpublishedEventsRecord>(
            $"drain:{correlationId}", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<UnpublishedEventsRecord>(true, record));
        ConfigureEventsInState(stateManager, eventCount: 1, correlationId: correlationId);
        _ = eventPublisher.PublishEventsAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromException<EventPublishResult>(new DaprException("pubsub unavailable")));

        Activity activity = await CaptureDrainActivityAsync(
            correlationId,
            () => actor.ReceiveReminderAsync($"drain-unpublished-{correlationId}", [], TimeSpan.Zero, TimeSpan.Zero));

        activity.GetTagItem("eventstore.failure_reason").ShouldBe(DrainReasonCodes.PublishFailed);
    }

    private static async Task<Activity> CaptureDrainActivityAsync(string correlationId, Func<Task> action) {
        var stopped = new List<Activity>();
        using var listener = new ActivityListener {
            ShouldListenTo = source => source.Name == EventStoreActivitySource.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => {
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
            host,
            logger,
            invoker,
            snapshotManager,
            new NoOpEventPayloadProtectionService(),
            statusStore,
            eventPublisher,
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
            host,
            logger,
            invoker,
            snapshotManager,
            new NoOpEventPayloadProtectionService(),
            statusStore,
            eventPublisher,
            Options.Create(new EventDrainOptions()),
            Options.Create(new BackpressureOptions()),
            Substitute.For<IDeadLetterPublisher>());
        ActorStateManagerTestHelper.SetStateManager(actor, stateManager);
        return (actor, stateManager, logger, eventPublisher, statusStore, timerManager);
    }

    private static void ConfigureEventsInState(
        IActorStateManager stateManager,
        int eventCount,
        string correlationId = "corr-drain",
        int startSequence = 1) {
        for (int seq = startSequence; seq < startSequence + eventCount; seq++) {
            var evt = new EventEnvelope(
                "msg-1",
                "agg-001",
                "test-aggregate",
                "test-tenant",
                "test-domain",
                seq,
                0,
                DateTimeOffset.UtcNow,
                correlationId,
                $"cause-{seq}",
                "user-1",
                "1.0.0",
                "OrderCreated",
                1,
                "json",
                [1, 2, 3],
                null);
            _ = stateManager.TryGetStateAsync<EventEnvelope>(
                $"test-tenant:test-domain:agg-001:events:{seq}", Arg.Any<CancellationToken>())
                .Returns(new ConditionalValue<EventEnvelope>(true, evt));
        }
    }
}
