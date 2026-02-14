namespace Hexalith.EventStore.IntegrationTests.Events;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Testing.Fakes;

using Microsoft.Extensions.Logging;

using NSubstitute;

using Shouldly;

using EventEnvelope = Hexalith.EventStore.Server.Events.EventEnvelope;

/// <summary>
/// Integration tests for event persistence using InMemoryStateManager.
/// Tests round-trip: EventPersister writes -> EventStreamReader reads.
/// </summary>
public class EventPersistenceIntegrationTests
{
    private static readonly AggregateIdentity TestIdentity = new("test-tenant", "test-domain", "agg-001");

    private sealed record OrderCreated(string OrderId, decimal Amount) : IEventPayload;

    private sealed record OrderItemAdded(string ItemId) : IEventPayload;

    private sealed record OrderRejected(string Reason) : IRejectionEvent;

    private static CommandEnvelope CreateTestCommand(
        string? correlationId = null,
        string? causationId = null) => new(
        TenantId: "test-tenant",
        Domain: "test-domain",
        AggregateId: "agg-001",
        CommandType: "CreateOrder",
        Payload: [1, 2, 3],
        CorrelationId: correlationId ?? "corr-001",
        CausationId: causationId ?? "cause-001",
        UserId: "user-1",
        Extensions: null);

    private static (EventPersister Persister, EventStreamReader Reader, InMemoryStateManager StateManager) CreateComponents()
    {
        var stateManager = new InMemoryStateManager();
        var persisterLogger = Substitute.For<ILogger<EventPersister>>();
        var readerLogger = Substitute.For<ILogger<EventStreamReader>>();
        return (
            new EventPersister(stateManager, persisterLogger),
            new EventStreamReader(stateManager, readerLogger),
            stateManager);
    }

    // === 8.1: Full pipeline persists events with correct keys and metadata ===

    [Fact]
    public async Task PersistAndRead_FullPipeline_EventsHaveCorrectKeysAndMetadata()
    {
        // Arrange
        (EventPersister persister, EventStreamReader reader, InMemoryStateManager stateManager) = CreateComponents();
        CommandEnvelope command = CreateTestCommand(correlationId: "corr-abc", causationId: "cause-xyz");
        var domainResult = DomainResult.Success(new IEventPayload[]
        {
            new OrderCreated("ORD-001", 99.99m),
        });

        // Act -- persist
        await persister.PersistEventsAsync(TestIdentity, command, domainResult, "v2");
        await stateManager.SaveStateAsync(); // Simulate AggregateActor atomic commit

        // Assert -- verify event stored at correct key
        stateManager.CommittedState.ShouldContainKey("test-tenant:test-domain:agg-001:events:1");
        var storedEnvelope = (EventEnvelope)stateManager.CommittedState["test-tenant:test-domain:agg-001:events:1"];
        storedEnvelope.AggregateId.ShouldBe("agg-001");
        storedEnvelope.TenantId.ShouldBe("test-tenant");
        storedEnvelope.Domain.ShouldBe("test-domain");
        storedEnvelope.SequenceNumber.ShouldBe(1);
        storedEnvelope.CorrelationId.ShouldBe("corr-abc");
        storedEnvelope.CausationId.ShouldBe("cause-xyz");
        storedEnvelope.UserId.ShouldBe("user-1");
        storedEnvelope.DomainServiceVersion.ShouldBe("v2");
        storedEnvelope.EventTypeName.ShouldContain("OrderCreated");
        storedEnvelope.SerializationFormat.ShouldBe("json");
        storedEnvelope.Payload.Length.ShouldBeGreaterThan(0);
    }

    // === 8.2: Multiple commands produce gapless sequence numbers ===

    [Fact]
    public async Task PersistMultipleCommands_GaplessSequenceNumbers()
    {
        // Arrange
        (EventPersister persister, EventStreamReader reader, InMemoryStateManager stateManager) = CreateComponents();

        // First command: 2 events
        CommandEnvelope cmd1 = CreateTestCommand(correlationId: "corr-1", causationId: "cause-1");
        var result1 = DomainResult.Success(new IEventPayload[]
        {
            new OrderCreated("ORD-001", 50.00m),
            new OrderItemAdded("ITEM-001"),
        });

        // Second command: 1 event
        CommandEnvelope cmd2 = CreateTestCommand(correlationId: "corr-2", causationId: "cause-2");
        var result2 = DomainResult.Success(new IEventPayload[]
        {
            new OrderItemAdded("ITEM-002"),
        });

        // Act -- persist both commands (simulating two actor turns)
        await persister.PersistEventsAsync(TestIdentity, cmd1, result1, "v1");
        await stateManager.SaveStateAsync();

        // Second persist reads updated metadata
        await persister.PersistEventsAsync(TestIdentity, cmd2, result2, "v1");
        await stateManager.SaveStateAsync();

        // Assert -- read all events via EventStreamReader
        object? state = await reader.RehydrateAsync(TestIdentity);
        var events = state.ShouldBeOfType<List<EventEnvelope>>();
        events.Count.ShouldBe(3);
        events[0].SequenceNumber.ShouldBe(1);
        events[1].SequenceNumber.ShouldBe(2);
        events[2].SequenceNumber.ShouldBe(3);
    }

    // === 8.3: Events readable after persistence via EventStreamReader (round-trip) ===

    [Fact]
    public async Task PersistAndRead_RoundTrip_EventsReadable()
    {
        // Arrange
        (EventPersister persister, EventStreamReader reader, InMemoryStateManager stateManager) = CreateComponents();
        CommandEnvelope command = CreateTestCommand();
        var domainResult = DomainResult.Success(new IEventPayload[]
        {
            new OrderCreated("ORD-001", 100.00m),
            new OrderItemAdded("ITEM-A"),
            new OrderItemAdded("ITEM-B"),
        });

        // Act
        await persister.PersistEventsAsync(TestIdentity, command, domainResult, "v1");
        await stateManager.SaveStateAsync();

        object? state = await reader.RehydrateAsync(TestIdentity);

        // Assert
        var events = state.ShouldBeOfType<List<EventEnvelope>>();
        events.Count.ShouldBe(3);
        events[0].EventTypeName.ShouldContain("OrderCreated");
        events[1].EventTypeName.ShouldContain("OrderItemAdded");
        events[2].EventTypeName.ShouldContain("OrderItemAdded");
    }

    // === 8.4: Atomic write -- all events visible together ===

    [Fact]
    public async Task PersistEvents_BeforeSave_NotVisibleInCommittedState()
    {
        // Arrange
        (EventPersister persister, EventStreamReader reader, InMemoryStateManager stateManager) = CreateComponents();
        CommandEnvelope command = CreateTestCommand();
        var domainResult = DomainResult.Success(new IEventPayload[]
        {
            new OrderCreated("ORD-001", 50.00m),
            new OrderItemAdded("ITEM-001"),
        });

        // Act -- persist but do NOT save yet
        await persister.PersistEventsAsync(TestIdentity, command, domainResult, "v1");

        // Assert -- committed state should not have events yet
        stateManager.CommittedState.ShouldNotContainKey("test-tenant:test-domain:agg-001:events:1");
        stateManager.CommittedState.ShouldNotContainKey("test-tenant:test-domain:agg-001:events:2");

        // Now save
        await stateManager.SaveStateAsync();

        // After save, all events visible together
        stateManager.CommittedState.ShouldContainKey("test-tenant:test-domain:agg-001:events:1");
        stateManager.CommittedState.ShouldContainKey("test-tenant:test-domain:agg-001:events:2");
        stateManager.CommittedState.ShouldContainKey(TestIdentity.MetadataKey);
    }

    [Fact]
    public async Task PersistEvents_MetadataUpdatedAtomically_WithEvents()
    {
        // Arrange
        (EventPersister persister, _, InMemoryStateManager stateManager) = CreateComponents();
        CommandEnvelope command = CreateTestCommand();
        var domainResult = DomainResult.Success(new IEventPayload[]
        {
            new OrderCreated("ORD-001", 75.00m),
        });

        // Act
        await persister.PersistEventsAsync(TestIdentity, command, domainResult, "v1");
        await stateManager.SaveStateAsync();

        // Assert -- metadata committed with correct sequence
        var metadata = (AggregateMetadata)stateManager.CommittedState[TestIdentity.MetadataKey];
        metadata.CurrentSequence.ShouldBe(1);
        metadata.LastModified.ShouldBeGreaterThan(DateTimeOffset.MinValue);
    }
}
