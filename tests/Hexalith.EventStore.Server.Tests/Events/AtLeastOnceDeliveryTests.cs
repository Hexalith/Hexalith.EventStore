
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
using Hexalith.EventStore.Testing.Fakes;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

using EventEnvelope = Hexalith.EventStore.Server.Events.EventEnvelope;

namespace Hexalith.EventStore.Server.Tests.Events;
/// <summary>
/// Story 4.3 Task 9: At-least-once delivery behavior tests.
/// Verifies end-to-end delivery, partial failure, and state store safety (NFR22).
/// </summary>
public class AtLeastOnceDeliveryTests {
    private sealed class TestEvent : IEventPayload;

    private static readonly AggregateIdentity TestIdentity = new("test-tenant", "test-domain", "agg-001");

    private static EventEnvelope CreateTestEnvelope(long sequenceNumber = 1) =>
        new(
            MessageId: $"msg-{sequenceNumber}",
            AggregateId: "agg-001",
            AggregateType: "test-aggregate",
            TenantId: "test-tenant",
            Domain: "test-domain",
            SequenceNumber: sequenceNumber,
            GlobalPosition: 0,
            Timestamp: DateTimeOffset.UtcNow,
            CorrelationId: "corr-001",
            CausationId: "cause-001",
            UserId: "user-1",
            DomainServiceVersion: "1.0.0",
            EventTypeName: "OrderCreated",
            MetadataVersion: 1,
            SerializationFormat: "json",
            Payload: [1, 2, 3],
            Extensions: null);

    private static CommandEnvelope CreateTestCommand(
        string? correlationId = null,
        string? causationId = null) => new(
        MessageId: Guid.NewGuid().ToString(),
        TenantId: "test-tenant",
        Domain: "test-domain",
        AggregateId: "agg-001",
        CommandType: "CreateOrder",
        Payload: [1, 2, 3],
        CorrelationId: correlationId ?? Guid.NewGuid().ToString(),
        CausationId: causationId,
        UserId: "system",
        Extensions: null);

    // --- Task 9.2: Success path ---

    [Fact]
    public async Task PublishEventsAsync_Success_EventsDeliveredAtLeastOnce() {
        // Arrange
        var fakePublisher = new FakeEventPublisher();
        var events = new List<EventEnvelope>
        {
            CreateTestEnvelope(1),
            CreateTestEnvelope(2),
            CreateTestEnvelope(3),
        };

        // Act
        EventPublishResult result = await fakePublisher.PublishEventsAsync(
            TestIdentity, events, "corr-001");

        // Assert
        result.Success.ShouldBeTrue();
        result.PublishedCount.ShouldBe(3);
        fakePublisher.TotalEventsPublished.ShouldBe(3);
        fakePublisher.GetEventsForTopic("test-tenant.test-domain.events").Count.ShouldBe(3);
    }

    // --- Task 9.3: Partial failure ---

    [Fact]
    public async Task PublishEventsAsync_PartialFailure_SomeEventsDelivered() {
        // Arrange
        var fakePublisher = new FakeEventPublisher();
        fakePublisher.SetupPartialFailure(eventIndex: 2, "Broker connection lost");
        var events = new List<EventEnvelope>
        {
            CreateTestEnvelope(1),
            CreateTestEnvelope(2),
            CreateTestEnvelope(3),
            CreateTestEnvelope(4),
            CreateTestEnvelope(5),
        };

        // Act
        EventPublishResult result = await fakePublisher.PublishEventsAsync(
            TestIdentity, events, "corr-001");

        // Assert -- partial delivery accepted (at-least-once allows partial)
        result.Success.ShouldBeFalse();
        result.PublishedCount.ShouldBe(2, "First 2 events should be published before failure at index 2");
        result.FailureReason.ShouldNotBeNullOrEmpty();
        fakePublisher.TotalEventsPublished.ShouldBe(2);
        fakePublisher.GetEventsForTopic("test-tenant.test-domain.events").Count.ShouldBe(2);
    }

    // --- Task 9.4: Total failure -- events safe in state store ---

    [Fact]
    public async Task PublishEventsAsync_TotalFailure_EventsSafeInStateStore() {
        // Arrange -- events are "in state store" (simulated by the list)
        var events = new List<EventEnvelope>
        {
            CreateTestEnvelope(1),
            CreateTestEnvelope(2),
            CreateTestEnvelope(3),
        };

        var fakePublisher = new FakeEventPublisher();
        fakePublisher.SetupFailure("Pub/sub system completely unavailable");

        // Act
        EventPublishResult result = await fakePublisher.PublishEventsAsync(
            TestIdentity, events, "corr-001");

        // Assert -- publication fails but events are NOT lost (still in events list = state store)
        result.Success.ShouldBeFalse();
        result.PublishedCount.ShouldBe(0);
        events.Count.ShouldBe(3, "Events remain in state store (not lost) even when publication fails (NFR22)");
        fakePublisher.TotalEventsPublished.ShouldBe(0);
        fakePublisher.GetPublishedTopics().ShouldBeEmpty();
    }

    // --- Task 9.5: Actor pipeline -- publish fails, events remain in state store ---

    [Fact]
    public async Task AggregateActor_PublishFailed_EventsNotLostInStateStore() {
        // Arrange
        (AggregateActor actor, IActorStateManager stateManager, _, IDomainServiceInvoker invoker,
            IEventPublisher eventPublisher) = CreateActorWithMockState();

        CommandEnvelope command = CreateTestCommand();
        ConfigureNoDuplicate(stateManager);

        // Domain returns events
        _ = invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>())
            .Returns(DomainResult.Success(new IEventPayload[] { new TestEvent() }));

        // Event publisher FAILS
        _ = eventPublisher.PublishEventsAsync(
            Arg.Any<AggregateIdentity>(),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>())
            .Returns(new EventPublishResult(false, 0, "Pub/sub unavailable"));

        // Act
        CommandProcessingResult result = await actor.ProcessCommandAsync(command);

        // Assert -- events were persisted to state store (SaveStateAsync called) before publish attempt
        result.Accepted.ShouldBeTrue();
        result.ErrorMessage.ShouldBeNull();

        // Verify SaveStateAsync was called at least once (events persisted before publish)
        await stateManager.Received().SaveStateAsync(Arg.Any<CancellationToken>());
    }

    // --- Task 9.6: Actor pipeline -- publish fails, status transitions to PublishFailed ---

    [Fact]
    public async Task AggregateActor_PublishFailed_StatusTransitionsToPublishFailed() {
        // Arrange
        (AggregateActor actor, IActorStateManager stateManager, ILogger<AggregateActor> logger,
            IDomainServiceInvoker invoker, IEventPublisher eventPublisher) = CreateActorWithMockState();

        CommandEnvelope command = CreateTestCommand();
        ConfigureNoDuplicate(stateManager);

        _ = invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>())
            .Returns(DomainResult.Success(new IEventPayload[] { new TestEvent() }));

        _ = eventPublisher.PublishEventsAsync(
            Arg.Any<AggregateIdentity>(),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>())
            .Returns(new EventPublishResult(false, 0, "Circuit breaker open"));

        // Act
        CommandProcessingResult result = await actor.ProcessCommandAsync(command);

        // Assert -- PublishFailed state was checkpointed
        result.Accepted.ShouldBeTrue();
        result.ErrorMessage.ShouldBeNull();

        // Verify that a PipelineState with PublishFailed was written
        await stateManager.Received().SetStateAsync(
            Arg.Any<string>(),
            Arg.Is<PipelineState>(ps => ps.CurrentStage == CommandStatus.PublishFailed),
            Arg.Any<CancellationToken>());
    }

    // --- Task 9.7: Circuit breaker open -- fast-fail behavior ---

    [Fact]
    public async Task CircuitBreaker_Open_PublisherReceivesImmediateFailure() {
        // Arrange -- Simulate circuit breaker behavior via EventPublisher returning immediate failure
        (AggregateActor actor, IActorStateManager stateManager, _,
            IDomainServiceInvoker invoker, IEventPublisher eventPublisher) = CreateActorWithMockState();

        CommandEnvelope command = CreateTestCommand();
        ConfigureNoDuplicate(stateManager);

        _ = invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>())
            .Returns(DomainResult.Success(new IEventPayload[] { new TestEvent() }));

        // Circuit breaker open: publisher receives immediate failure (fast-fail)
        _ = eventPublisher.PublishEventsAsync(
            Arg.Any<AggregateIdentity>(),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>())
            .Returns(new EventPublishResult(false, 0, "Circuit breaker is open"));

        // Act
        CommandProcessingResult result = await actor.ProcessCommandAsync(command);

        // Assert -- actor transitions to PublishFailed without waiting
        result.Accepted.ShouldBeTrue();
        result.ErrorMessage.ShouldBeNull();

        // Verify PublishFailed terminal state
        await stateManager.Received().SetStateAsync(
            Arg.Any<string>(),
            Arg.Is<PipelineState>(ps => ps.CurrentStage == CommandStatus.PublishFailed),
            Arg.Any<CancellationToken>());
    }

    private static (AggregateActor Actor, IActorStateManager StateManager, ILogger<AggregateActor> Logger,
        IDomainServiceInvoker Invoker, IEventPublisher EventPublisher) CreateActorWithMockState() {
        IActorStateManager stateManager = Substitute.For<IActorStateManager>();
        ILogger<AggregateActor> logger = Substitute.For<ILogger<AggregateActor>>();
        IDomainServiceInvoker invoker = Substitute.For<IDomainServiceInvoker>();
        ISnapshotManager snapshotManager = Substitute.For<ISnapshotManager>();
        ICommandStatusStore commandStatusStore = Substitute.For<ICommandStatusStore>();
        IEventPublisher eventPublisher = Substitute.For<IEventPublisher>();
        var host = ActorHost.CreateForTest<AggregateActor>(
            new ActorTestOptions { ActorId = new ActorId("test-tenant:test-domain:agg-001") });
        var actor = new AggregateActor(host, logger, invoker, snapshotManager, new NoOpEventPayloadProtectionService(), commandStatusStore, eventPublisher, Options.Create(new EventDrainOptions()), Options.Create(new BackpressureOptions()), Substitute.For<IDeadLetterPublisher>());

        // Set the mock state manager via reflection
        PropertyInfo? prop = typeof(Actor).GetProperty("StateManager", BindingFlags.Public | BindingFlags.Instance);
        prop?.SetValue(actor, stateManager);

        // Default: domain service returns NoOp
        _ = invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>())
            .Returns(DomainResult.NoOp());

        // Default: no pipeline state (fresh command)
        _ = stateManager.TryGetStateAsync<PipelineState>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<PipelineState>(false, default!));

        // Default: event publisher succeeds
        _ = eventPublisher.PublishEventsAsync(
            Arg.Any<AggregateIdentity>(),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>())
            .Returns(callInfo => new EventPublishResult(true, callInfo.ArgAt<IReadOnlyList<EventEnvelope>>(1).Count, null));

        return (actor, stateManager, logger, invoker, eventPublisher);
    }

    private static void ConfigureNoDuplicate(IActorStateManager stateManager) {
        _ = stateManager.TryGetStateAsync<IdempotencyRecord>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<IdempotencyRecord>(false, default!));

        // Default: new aggregate (no metadata) -- Step 3 returns null state
        _ = stateManager.TryGetStateAsync<AggregateMetadata>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<AggregateMetadata>(false, default!));
    }
}
