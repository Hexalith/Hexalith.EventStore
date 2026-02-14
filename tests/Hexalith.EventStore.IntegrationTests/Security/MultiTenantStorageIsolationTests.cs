namespace Hexalith.EventStore.IntegrationTests.Security;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Testing.Assertions;
using Hexalith.EventStore.Testing.Fakes;

using Microsoft.Extensions.Logging;

using NSubstitute;

using Shouldly;

using EventEnvelope = Hexalith.EventStore.Server.Events.EventEnvelope;

/// <summary>
/// Component-level integration tests verifying multi-tenant storage isolation through
/// the event persistence pipeline (AC: #1, #4, #5, #7, #9).
/// Uses in-memory fakes to exercise multiple components together (EventPersister, EventStreamReader,
/// IdempotencyChecker, AggregateIdentity) without requiring DAPR runtime. Classified as integration
/// tests because they validate cross-component behavior, not individual class logic.
/// </summary>
public class MultiTenantStorageIsolationTests
{
    private sealed record OrderCreated(string OrderId, decimal Amount) : IEventPayload;

    private sealed record OrderItemAdded(string ItemId) : IEventPayload;

    private static CommandEnvelope CreateTestCommand(
        string tenantId,
        string domain = "orders",
        string aggregateId = "order-001",
        string? correlationId = null,
        string? causationId = null) => new(
        TenantId: tenantId,
        Domain: domain,
        AggregateId: aggregateId,
        CommandType: "CreateOrder",
        Payload: [1, 2, 3],
        CorrelationId: correlationId ?? Guid.NewGuid().ToString(),
        CausationId: causationId ?? Guid.NewGuid().ToString(),
        UserId: "test-user",
        Extensions: null);

    private static (EventPersister Persister, EventStreamReader Reader, InMemoryStateManager StateManager) CreatePipelineForTenant()
    {
        var stateManager = new InMemoryStateManager();
        var persisterLogger = Substitute.For<ILogger<EventPersister>>();
        var readerLogger = Substitute.For<ILogger<EventStreamReader>>();
        return (
            new EventPersister(stateManager, persisterLogger),
            new EventStreamReader(stateManager, readerLogger),
            stateManager);
    }

    // --- 3.2: Commands for tenant-a and tenant-b produce events with non-overlapping state store keys ---

    [Fact]
    public async Task TwoTenants_ProduceNonOverlappingStateStoreKeys()
    {
        // Arrange - each tenant gets its own state manager (simulating separate actor instances)
        (EventPersister persisterA, _, InMemoryStateManager stateManagerA) = CreatePipelineForTenant();
        (EventPersister persisterB, _, InMemoryStateManager stateManagerB) = CreatePipelineForTenant();

        var identityA = new AggregateIdentity("tenant-a", "orders", "order-001");
        var identityB = new AggregateIdentity("tenant-b", "orders", "order-001");

        CommandEnvelope cmdA = CreateTestCommand("tenant-a");
        CommandEnvelope cmdB = CreateTestCommand("tenant-b");

        var domainResult = DomainResult.Success(new IEventPayload[]
        {
            new OrderCreated("ORD-001", 100.00m),
        });

        // Act
        await persisterA.PersistEventsAsync(identityA, cmdA, domainResult, "v1");
        await stateManagerA.SaveStateAsync();

        await persisterB.PersistEventsAsync(identityB, cmdB, domainResult, "v1");
        await stateManagerB.SaveStateAsync();

        // Assert - keys in each state manager are tenant-prefixed correctly
        IEnumerable<string> keysA = stateManagerA.CommittedState.Keys;
        IEnumerable<string> keysB = stateManagerB.CommittedState.Keys;

        foreach (string key in keysA)
        {
            StorageKeyIsolationAssertions.AssertKeyBelongsToTenant(key, "tenant-a");
            key.ShouldNotStartWith("tenant-b:");
        }

        foreach (string key in keysB)
        {
            StorageKeyIsolationAssertions.AssertKeyBelongsToTenant(key, "tenant-b");
            key.ShouldNotStartWith("tenant-a:");
        }

        // No key in A's state store appears in B's state store
        keysA.ShouldNotContain(k => stateManagerB.CommittedState.ContainsKey(k));
        keysB.ShouldNotContain(k => stateManagerA.CommittedState.ContainsKey(k));
    }

    // --- 3.3: Actor for tenant-a cannot access state store keys belonging to tenant-b ---

    [Fact]
    public async Task TenantA_CannotAccess_TenantB_StateStoreKeys()
    {
        // Arrange - separate state managers simulate DAPR actor scope isolation
        (EventPersister persisterA, EventStreamReader readerA, InMemoryStateManager stateManagerA) = CreatePipelineForTenant();
        (EventPersister persisterB, _, InMemoryStateManager stateManagerB) = CreatePipelineForTenant();

        var identityB = new AggregateIdentity("tenant-b", "orders", "order-001");

        CommandEnvelope cmdB = CreateTestCommand("tenant-b");
        var domainResult = DomainResult.Success(new IEventPayload[]
        {
            new OrderCreated("ORD-001", 200.00m),
        });

        // Persist events for tenant-b
        await persisterB.PersistEventsAsync(identityB, cmdB, domainResult, "v1");
        await stateManagerB.SaveStateAsync();

        // Act - attempt to read tenant-b's events from tenant-a's state manager
        var identityBFromA = new AggregateIdentity("tenant-b", "orders", "order-001");
        RehydrationResult? stateFromA = await readerA.RehydrateAsync(identityBFromA);

        // Assert - tenant-a's state manager has no data for tenant-b
        stateFromA.ShouldBeNull();
    }

    // --- 3.4: Idempotency records for tenant-a are invisible to tenant-b's actor instance ---

    [Fact]
    public async Task IdempotencyRecords_ForTenantA_InvisibleTo_TenantB()
    {
        // Arrange - separate state managers per actor instance
        var stateManagerA = new InMemoryStateManager();
        var stateManagerB = new InMemoryStateManager();

        var idempotencyA = new IdempotencyChecker(stateManagerA, Substitute.For<ILogger<IdempotencyChecker>>());
        var idempotencyB = new IdempotencyChecker(stateManagerB, Substitute.For<ILogger<IdempotencyChecker>>());

        string causationId = "shared-causation-id";
        var result = new CommandProcessingResult(true, CorrelationId: "corr-001");

        // Act - record idempotency in tenant-a's actor
        await idempotencyA.RecordAsync(causationId, result);
        await stateManagerA.SaveStateAsync();

        // Check from tenant-b's actor
        CommandProcessingResult? checkFromB = await idempotencyB.CheckAsync(causationId);

        // Assert - tenant-b cannot see tenant-a's idempotency record
        checkFromB.ShouldBeNull();

        // But tenant-a can see its own record
        CommandProcessingResult? checkFromA = await idempotencyA.CheckAsync(causationId);
        checkFromA.ShouldNotBeNull();
    }

    // --- 3.5: Full pipeline test -- submit commands for two tenants, verify complete key isolation ---

    [Fact]
    public async Task FullPipeline_TwoTenants_CompleteKeyIsolation()
    {
        // Arrange
        (EventPersister persisterA, EventStreamReader readerA, InMemoryStateManager stateManagerA) = CreatePipelineForTenant();
        (EventPersister persisterB, EventStreamReader readerB, InMemoryStateManager stateManagerB) = CreatePipelineForTenant();

        var identityA = new AggregateIdentity("tenant-a", "orders", "order-001");
        var identityB = new AggregateIdentity("tenant-b", "orders", "order-001");

        CommandEnvelope cmdA = CreateTestCommand("tenant-a", correlationId: "corrA");
        CommandEnvelope cmdB = CreateTestCommand("tenant-b", correlationId: "corrB");

        var resultA = DomainResult.Success(new IEventPayload[]
        {
            new OrderCreated("ORD-A", 100.00m),
            new OrderItemAdded("ITEM-A1"),
        });

        var resultB = DomainResult.Success(new IEventPayload[]
        {
            new OrderCreated("ORD-B", 200.00m),
        });

        // Act - persist events for both tenants
        await persisterA.PersistEventsAsync(identityA, cmdA, resultA, "v1");
        await stateManagerA.SaveStateAsync();

        await persisterB.PersistEventsAsync(identityB, cmdB, resultB, "v1");
        await stateManagerB.SaveStateAsync();

        // Assert - each tenant's reader only sees its own events
        RehydrationResult? stateA = await readerA.RehydrateAsync(identityA);
        stateA.ShouldNotBeNull();
        stateA.Events.Count.ShouldBe(2);
        stateA.Events.ShouldAllBe(e => e.TenantId == "tenant-a");

        RehydrationResult? stateB = await readerB.RehydrateAsync(identityB);
        stateB.ShouldNotBeNull();
        stateB.Events.Count.ShouldBe(1);
        stateB.Events.ShouldAllBe(e => e.TenantId == "tenant-b");

        // Cross-tenant reads return null
        RehydrationResult? crossA = await readerA.RehydrateAsync(identityB);
        crossA.ShouldBeNull();

        RehydrationResult? crossB = await readerB.RehydrateAsync(identityA);
        crossB.ShouldBeNull();
    }

    // --- 3.6: NEGATIVE -- explicitly attempt to read tenant-b's event from tenant-a's actor ---

    [Fact]
    public async Task Negative_ReadTenantBEvent_FromTenantAContext_ReturnsNull()
    {
        // Arrange
        (EventPersister persisterB, _, InMemoryStateManager stateManagerB) = CreatePipelineForTenant();
        var stateManagerA = new InMemoryStateManager();

        var identityB = new AggregateIdentity("tenant-b", "orders", "order-001");
        CommandEnvelope cmdB = CreateTestCommand("tenant-b");
        var domainResult = DomainResult.Success(new IEventPayload[]
        {
            new OrderCreated("ORD-B", 500.00m),
        });

        await persisterB.PersistEventsAsync(identityB, cmdB, domainResult, "v1");
        await stateManagerB.SaveStateAsync();

        // Act - explicitly try to read tenant-b's event key from tenant-a's state manager
        string tenantBEventKey = $"{identityB.EventStreamKeyPrefix}1";
        var result = await stateManagerA.TryGetStateAsync<EventEnvelope>(tenantBEventKey);

        // Assert
        result.HasValue.ShouldBeFalse();
    }

    // --- 3.7: NEGATIVE -- submit command for tenant-a, verify tenant-b cannot retrieve events ---

    [Fact]
    public async Task Negative_SubmitForTenantA_TenantB_CannotRetrieveEvents()
    {
        // Arrange
        (EventPersister persisterA, _, InMemoryStateManager stateManagerA) = CreatePipelineForTenant();
        var stateManagerB = new InMemoryStateManager();
        var readerB = new EventStreamReader(stateManagerB, Substitute.For<ILogger<EventStreamReader>>());

        var identityA = new AggregateIdentity("tenant-a", "orders", "order-001");
        CommandEnvelope cmdA = CreateTestCommand("tenant-a");
        var domainResult = DomainResult.Success(new IEventPayload[]
        {
            new OrderCreated("ORD-A", 300.00m),
        });

        await persisterA.PersistEventsAsync(identityA, cmdA, domainResult, "v1");
        await stateManagerA.SaveStateAsync();

        // Act - tenant-b attempts to read tenant-a's events
        RehydrationResult? stateFromB = await readerB.RehydrateAsync(identityA);

        // Assert
        stateFromB.ShouldBeNull();

        // Also verify using tenant-b's identity but same aggregate name
        var identityBSameName = new AggregateIdentity("tenant-b", "orders", "order-001");
        RehydrationResult? stateFromBSameName = await readerB.RehydrateAsync(identityBSameName);
        stateFromBSameName.ShouldBeNull();
    }
}
