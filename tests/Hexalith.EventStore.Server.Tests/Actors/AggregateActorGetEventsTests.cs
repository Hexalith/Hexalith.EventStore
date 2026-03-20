
using System.Reflection;

using Dapr.Actors;
using Dapr.Actors.Runtime;

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

namespace Hexalith.EventStore.Server.Tests.Actors;

public class AggregateActorGetEventsTests {
    private const string ActorIdString = "tenant-a:counter:counter-1";
    private const string MetadataKey = "tenant-a:counter:counter-1:metadata";
    private const string EventKeyPrefix = "tenant-a:counter:counter-1:events:";

    private static (AggregateActor Actor, IActorStateManager StateManager) CreateActor() {
        IActorStateManager stateManager = Substitute.For<IActorStateManager>();
        ILogger<AggregateActor> logger = Substitute.For<ILogger<AggregateActor>>();
        _ = logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);
        IDomainServiceInvoker invoker = Substitute.For<IDomainServiceInvoker>();
        ISnapshotManager snapshotManager = Substitute.For<ISnapshotManager>();
        ICommandStatusStore commandStatusStore = Substitute.For<ICommandStatusStore>();
        IEventPublisher eventPublisher = Substitute.For<IEventPublisher>();
        IDeadLetterPublisher deadLetterPublisher = Substitute.For<IDeadLetterPublisher>();

        var host = ActorHost.CreateForTest<AggregateActor>(
            new ActorTestOptions { ActorId = new ActorId(ActorIdString) });

        var actor = new AggregateActor(
            host, logger, invoker, snapshotManager,
            new NoOpEventPayloadProtectionService(), commandStatusStore, eventPublisher,
            Options.Create(new EventDrainOptions()),
            Options.Create(new BackpressureOptions()),
            deadLetterPublisher);

        PropertyInfo? prop = typeof(Actor).GetProperty("StateManager", BindingFlags.Public | BindingFlags.Instance);
        prop?.SetValue(actor, stateManager);

        return (actor, stateManager);
    }

    private static EventEnvelope CreateEvent(int seq) => new(
        MessageId: $"msg-{seq}",
        AggregateId: "counter-1",
        AggregateType: "counter",
        TenantId: "tenant-a",
        Domain: "counter",
        SequenceNumber: seq,
        GlobalPosition: 0,
        Timestamp: DateTimeOffset.UtcNow,
        CorrelationId: $"corr-{seq}",
        CausationId: $"cause-{seq}",
        UserId: "user-1",
        DomainServiceVersion: "1.0.0",
        EventTypeName: "CounterIncremented",
        MetadataVersion: 1,
        SerializationFormat: "json",
        Payload: [1, 2, 3],
        Extensions: null);

    private static void ConfigureMetadata(IActorStateManager stateManager, long currentSequence) {
        var metadata = new AggregateMetadata(currentSequence, DateTimeOffset.UtcNow, null);
        _ = stateManager.TryGetStateAsync<AggregateMetadata>(MetadataKey, Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<AggregateMetadata>(true, metadata));
    }

    private static void ConfigureEvents(IActorStateManager stateManager, int count) {
        for (int i = 1; i <= count; i++) {
            int seq = i;
            EventEnvelope evt = CreateEvent(seq);
            _ = stateManager.TryGetStateAsync<EventEnvelope>($"{EventKeyPrefix}{seq}", Arg.Any<CancellationToken>())
                .Returns(new ConditionalValue<EventEnvelope>(true, evt));
        }
    }

    [Fact]
    public async Task GetEventsAsync_NewAggregate_ReturnsEmptyArray() {
        (AggregateActor actor, IActorStateManager stateManager) = CreateActor();
        _ = stateManager.TryGetStateAsync<AggregateMetadata>(MetadataKey, Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<AggregateMetadata>(false, default!));

        EventEnvelope[] result = await actor.GetEventsAsync(0);

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetEventsAsync_WithEvents_ReturnsEventsAfterSequence() {
        (AggregateActor actor, IActorStateManager stateManager) = CreateActor();
        ConfigureMetadata(stateManager, 3);
        ConfigureEvents(stateManager, 3);

        EventEnvelope[] result = await actor.GetEventsAsync(1);

        result.Length.ShouldBe(2);
        result[0].SequenceNumber.ShouldBe(2);
        result[1].SequenceNumber.ShouldBe(3);
    }

    [Fact]
    public async Task GetEventsAsync_FromSequenceAtCurrent_ReturnsEmpty() {
        (AggregateActor actor, IActorStateManager stateManager) = CreateActor();
        ConfigureMetadata(stateManager, 3);

        EventEnvelope[] result = await actor.GetEventsAsync(3);

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetEventsAsync_FromSequenceBeyondCurrent_ReturnsEmpty() {
        (AggregateActor actor, IActorStateManager stateManager) = CreateActor();
        ConfigureMetadata(stateManager, 3);

        EventEnvelope[] result = await actor.GetEventsAsync(100);

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetEventsAsync_ExactlyMaxBatchSize_ReturnsAllEvents() {
        (AggregateActor actor, IActorStateManager stateManager) = CreateActor();
        ConfigureMetadata(stateManager, 32);
        ConfigureEvents(stateManager, 32);

        EventEnvelope[] result = await actor.GetEventsAsync(0);

        result.Length.ShouldBe(32);
        for (int i = 0; i < 32; i++) {
            result[i].SequenceNumber.ShouldBe(i + 1);
        }
    }

    [Fact]
    public async Task GetEventsAsync_MoreThanMaxBatchSize_ReturnsAllEvents() {
        (AggregateActor actor, IActorStateManager stateManager) = CreateActor();
        ConfigureMetadata(stateManager, 33);
        ConfigureEvents(stateManager, 33);

        EventEnvelope[] result = await actor.GetEventsAsync(0);

        result.Length.ShouldBe(33);
        for (int i = 0; i < 33; i++) {
            result[i].SequenceNumber.ShouldBe(i + 1);
        }
    }

    [Fact]
    public async Task GetEventsAsync_MissingEventKey_ThrowsMissingEventException() {
        (AggregateActor actor, IActorStateManager stateManager) = CreateActor();
        ConfigureMetadata(stateManager, 3);

        // Configure event 1 and 3 but NOT event 2
        EventEnvelope evt1 = CreateEvent(1);
        _ = stateManager.TryGetStateAsync<EventEnvelope>($"{EventKeyPrefix}1", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<EventEnvelope>(true, evt1));
        _ = stateManager.TryGetStateAsync<EventEnvelope>($"{EventKeyPrefix}2", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<EventEnvelope>(false, default!));
        EventEnvelope evt3 = CreateEvent(3);
        _ = stateManager.TryGetStateAsync<EventEnvelope>($"{EventKeyPrefix}3", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<EventEnvelope>(true, evt3));

        MissingEventException ex = await Should.ThrowAsync<MissingEventException>(
            () => actor.GetEventsAsync(0));

        ex.SequenceNumber.ShouldBe(2);
    }

    [Fact]
    public async Task GetEventsAsync_NegativeFromSequence_ClampsToZero() {
        (AggregateActor actor, IActorStateManager stateManager) = CreateActor();
        ConfigureMetadata(stateManager, 3);
        ConfigureEvents(stateManager, 3);

        EventEnvelope[] result = await actor.GetEventsAsync(-1);

        result.Length.ShouldBe(3);
        result[0].SequenceNumber.ShouldBe(1);
        result[1].SequenceNumber.ShouldBe(2);
        result[2].SequenceNumber.ShouldBe(3);
    }
}
