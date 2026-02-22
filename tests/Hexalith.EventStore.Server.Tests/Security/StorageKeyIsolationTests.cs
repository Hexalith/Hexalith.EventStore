
using Dapr.Actors;
using Dapr.Actors.Client;
using Dapr.Actors.Runtime;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Pipeline.Commands;
using Hexalith.EventStore.Testing.Assertions;
using Hexalith.EventStore.Testing.Fakes;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Security;
/// <summary>
/// Comprehensive unit tests verifying storage key isolation between tenants (AC: #1, #2, #3, #4, #6, #9, #10).
/// </summary>
public class StorageKeyIsolationTests {
    // --- 2.2: Two different tenants produce structurally disjoint event stream key prefixes ---

    [Fact]
    public void DifferentTenants_ProduceDisjointEventStreamKeyPrefixes() {
        // Arrange
        var identityA = new AggregateIdentity("tenant-a", "orders", "order-001");
        var identityB = new AggregateIdentity("tenant-b", "orders", "order-001");

        // Act
        string prefixA = identityA.EventStreamKeyPrefix;
        string prefixB = identityB.EventStreamKeyPrefix;

        // Assert
        prefixA.ShouldStartWith("tenant-a:");
        prefixB.ShouldStartWith("tenant-b:");
        prefixA.ShouldNotBe(prefixB);
        StorageKeyIsolationAssertions.AssertKeyBelongsToTenant(prefixA, "tenant-a");
        StorageKeyIsolationAssertions.AssertKeyBelongsToTenant(prefixB, "tenant-b");
    }

    // --- 2.3: Two different tenants produce structurally disjoint metadata keys ---

    [Fact]
    public void DifferentTenants_ProduceDisjointMetadataKeys() {
        // Arrange
        var identityA = new AggregateIdentity("tenant-a", "orders", "order-001");
        var identityB = new AggregateIdentity("tenant-b", "orders", "order-001");

        // Act
        string metaA = identityA.MetadataKey;
        string metaB = identityB.MetadataKey;

        // Assert
        metaA.ShouldStartWith("tenant-a:");
        metaB.ShouldStartWith("tenant-b:");
        metaA.ShouldNotBe(metaB);
        StorageKeyIsolationAssertions.AssertKeysDisjoint(metaA, metaB);
    }

    // --- 2.4: Two different tenants produce structurally disjoint snapshot keys ---

    [Fact]
    public void DifferentTenants_ProduceDisjointSnapshotKeys() {
        // Arrange
        var identityA = new AggregateIdentity("tenant-a", "orders", "order-001");
        var identityB = new AggregateIdentity("tenant-b", "orders", "order-001");

        // Act
        string snapA = identityA.SnapshotKey;
        string snapB = identityB.SnapshotKey;

        // Assert
        snapA.ShouldStartWith("tenant-a:");
        snapB.ShouldStartWith("tenant-b:");
        snapA.ShouldNotBe(snapB);
        StorageKeyIsolationAssertions.AssertKeysDisjoint(snapA, snapB);
    }

    // --- 2.5: Same aggregate ID in different tenants produces different keys (no collision) ---

    [Fact]
    public void SameAggregateId_DifferentTenants_ProduceDifferentKeys() {
        // Arrange
        var identityA = new AggregateIdentity("tenant-a", "orders", "shared-agg-001");
        var identityB = new AggregateIdentity("tenant-b", "orders", "shared-agg-001");

        // Act & Assert - all key types differ
        identityA.EventStreamKeyPrefix.ShouldNotBe(identityB.EventStreamKeyPrefix);
        identityA.MetadataKey.ShouldNotBe(identityB.MetadataKey);
        identityA.SnapshotKey.ShouldNotBe(identityB.SnapshotKey);
        identityA.ActorId.ShouldNotBe(identityB.ActorId);
    }

    // --- 2.6: Same aggregate ID in same tenant but different domains produces different keys ---

    [Fact]
    public void SameAggregateId_SameTenant_DifferentDomains_ProduceDifferentKeys() {
        // Arrange
        var identityOrders = new AggregateIdentity("tenant-a", "orders", "item-001");
        var identityInventory = new AggregateIdentity("tenant-a", "inventory", "item-001");

        // Act & Assert - all key types differ
        identityOrders.EventStreamKeyPrefix.ShouldNotBe(identityInventory.EventStreamKeyPrefix);
        identityOrders.MetadataKey.ShouldNotBe(identityInventory.MetadataKey);
        identityOrders.SnapshotKey.ShouldNotBe(identityInventory.SnapshotKey);
        identityOrders.ActorId.ShouldNotBe(identityInventory.ActorId);
    }

    // --- 2.7: AggregateIdentity rejects tenant IDs containing colons (key injection prevention) ---

    [Fact]
    public void AggregateIdentity_RejectsTenantIdWithColons() =>
        // Act & Assert
        Should.Throw<ArgumentException>(
            () => new AggregateIdentity("tenant:injected", "orders", "order-001"));

    // --- 2.8: AggregateIdentity rejects domain names containing colons ---

    [Fact]
    public void AggregateIdentity_RejectsDomainWithColons() =>
        // Act & Assert
        Should.Throw<ArgumentException>(
            () => new AggregateIdentity("tenant-a", "orders:fake", "order-001"));

    // --- 2.9: AggregateIdentity rejects aggregate IDs containing colons ---

    [Fact]
    public void AggregateIdentity_RejectsAggregateIdWithColons() =>
        // Act & Assert
        Should.Throw<ArgumentException>(
            () => new AggregateIdentity("tenant-a", "orders", "order:injected"));

    // --- 2.10: Key prefix {tenantA}: never appears as a prefix of any key derived for {tenantB} ---

    [Theory]
    [InlineData("tenant-a", "tenant-b")]
    [InlineData("acme", "globex")]
    [InlineData("a", "ab")]
    public void TenantPrefixNeverAppearsInOtherTenantsKeys(string tenantA, string tenantB) {
        // Arrange
        var identityA = new AggregateIdentity(tenantA, "orders", "order-001");
        var identityB = new AggregateIdentity(tenantB, "orders", "order-001");
        string prefixA = $"{tenantA}:";
        string prefixB = $"{tenantB}:";

        // Act & Assert - none of B's keys start with A's prefix
        identityB.EventStreamKeyPrefix.ShouldNotStartWith(prefixA);
        identityB.MetadataKey.ShouldNotStartWith(prefixA);
        identityB.SnapshotKey.ShouldNotStartWith(prefixA);
        identityB.ActorId.ShouldNotStartWith(prefixA);

        // And vice versa
        identityA.EventStreamKeyPrefix.ShouldNotStartWith(prefixB);
        identityA.MetadataKey.ShouldNotStartWith(prefixB);
        identityA.SnapshotKey.ShouldNotStartWith(prefixB);
        identityA.ActorId.ShouldNotStartWith(prefixB);
    }

    // --- 2.11: StorageKeyIsolationAssertions correctly validates keys belong to expected tenant ---

    [Fact]
    public void StorageKeyIsolationAssertions_ValidatesCorrectTenant() {
        // Arrange
        var identity = new AggregateIdentity("tenant-a", "orders", "order-001");

        // Act & Assert - all assertions should pass for correct tenant
        StorageKeyIsolationAssertions.AssertKeyBelongsToTenant(identity.EventStreamKeyPrefix, "tenant-a");
        StorageKeyIsolationAssertions.AssertKeyBelongsToTenant(identity.MetadataKey, "tenant-a");
        StorageKeyIsolationAssertions.AssertKeyBelongsToTenant(identity.SnapshotKey, "tenant-a");

        // Also validate full event stream key
        string eventKey = $"{identity.EventStreamKeyPrefix}1";
        StorageKeyIsolationAssertions.AssertEventStreamKey(eventKey, identity);
    }

    // --- 2.12: StorageKeyIsolationAssertions rejects keys that don't match expected tenant ---

    [Fact]
    public void StorageKeyIsolationAssertions_RejectsWrongTenant() {
        // Arrange
        var identity = new AggregateIdentity("tenant-a", "orders", "order-001");

        // Act & Assert - assertion should fail for wrong tenant
        _ = Should.Throw<ShouldAssertException>(
            () => StorageKeyIsolationAssertions.AssertKeyBelongsToTenant(identity.EventStreamKeyPrefix, "tenant-b"));
    }

    [Fact]
    public void StorageKeyIsolationAssertions_AssertKeysDisjoint_PassesForDifferentTenants() {
        // Arrange
        var identityA = new AggregateIdentity("tenant-a", "orders", "order-001");
        var identityB = new AggregateIdentity("tenant-b", "orders", "order-001");

        // Act & Assert - should pass (different tenants)
        StorageKeyIsolationAssertions.AssertKeysDisjoint(identityA.MetadataKey, identityB.MetadataKey);
    }

    [Fact]
    public void StorageKeyIsolationAssertions_AssertKeysDisjoint_FailsForSameTenant() {
        // Arrange
        var identityA = new AggregateIdentity("tenant-a", "orders", "order-001");
        var identityB = new AggregateIdentity("tenant-a", "inventory", "item-001");

        // Act & Assert - should fail (same tenant prefix)
        _ = Should.Throw<ShouldAssertException>(
            () => StorageKeyIsolationAssertions.AssertKeysDisjoint(identityA.MetadataKey, identityB.MetadataKey));
    }

    // --- 2.13: CommandRouter always uses AggregateIdentity.ActorId for actor ID derivation (AC: #10) ---

    [Theory]
    [InlineData("tenant-a", "orders", "order-001")]
    [InlineData("tenant-b", "inventory", "item-999")]
    [InlineData("acme", "billing", "inv-ABC123")]
    public async Task CommandRouter_AlwaysUsesAggregateIdentityActorId(string tenant, string domain, string aggregateId) {
        // Arrange
        ActorId? capturedActorId = null;
        IAggregateActor actorProxy = Substitute.For<IAggregateActor>();
        _ = actorProxy.ProcessCommandAsync(Arg.Any<CommandEnvelope>())
            .Returns(new CommandProcessingResult(true));

        IActorProxyFactory proxyFactory = Substitute.For<IActorProxyFactory>();
        _ = proxyFactory.CreateActorProxy<IAggregateActor>(Arg.Do<ActorId>(id => capturedActorId = id), Arg.Any<string>())
            .Returns(actorProxy);

        var router = new CommandRouter(proxyFactory, NullLogger<CommandRouter>.Instance);
        var expectedIdentity = new AggregateIdentity(tenant, domain, aggregateId);
        var command = new SubmitCommand(
            Tenant: tenant,
            Domain: domain,
            AggregateId: aggregateId,
            CommandType: "TestCommand",
            Payload: [1, 2, 3],
            CorrelationId: Guid.NewGuid().ToString(),
            UserId: "test-user");

        // Act
        _ = await router.RouteCommandAsync(command);

        // Assert - ActorId matches AggregateIdentity.ActorId exactly
        _ = capturedActorId.ShouldNotBeNull();
        capturedActorId.ToString().ShouldBe(expectedIdentity.ActorId);
    }

    // --- 2.14: Tenant value chain-of-custody ---

    [Fact]
    public void TenantChainOfCustody_ConsistentThroughAllDerivedKeys() {
        // Arrange
        const string tenantId = "tenant-alpha";
        var identity = new AggregateIdentity(tenantId, "orders", "order-001");

        // Act & Assert - tenant is consistently the first segment in all derived keys
        identity.TenantId.ShouldBe(tenantId);
        identity.ActorId.ShouldStartWith($"{tenantId}:");
        identity.EventStreamKeyPrefix.ShouldStartWith($"{tenantId}:");
        identity.MetadataKey.ShouldStartWith($"{tenantId}:");
        identity.SnapshotKey.ShouldStartWith($"{tenantId}:");

        // The tenant value is the first colon-delimited segment everywhere
        string actorIdTenant = identity.ActorId.Split(':')[0];
        string eventKeyTenant = identity.EventStreamKeyPrefix.Split(':')[0];
        string metaKeyTenant = identity.MetadataKey.Split(':')[0];
        string snapKeyTenant = identity.SnapshotKey.Split(':')[0];

        actorIdTenant.ShouldBe(tenantId);
        eventKeyTenant.ShouldBe(tenantId);
        metaKeyTenant.ShouldBe(tenantId);
        snapKeyTenant.ShouldBe(tenantId);
    }

    // --- 2.15: URL-encoded colons (%3A) rejected by AggregateIdentity regex ---

    [Theory]
    [InlineData("tenant%3ainjected")]
    [InlineData("tenant%3Ainjected")]
    public void AggregateIdentity_RejectsUrlEncodedColonsInTenant(string maliciousTenant) =>
        // Act & Assert - % is not in allowed character set
        Should.Throw<ArgumentException>(
            () => new AggregateIdentity(maliciousTenant, "orders", "order-001"));

    [Theory]
    [InlineData("orders%3afake")]
    [InlineData("orders%3Afake")]
    public void AggregateIdentity_RejectsUrlEncodedColonsInDomain(string maliciousDomain) =>
        // Act & Assert
        Should.Throw<ArgumentException>(
            () => new AggregateIdentity("tenant-a", maliciousDomain, "order-001"));

    [Theory]
    [InlineData("order%3Ainjected")]
    [InlineData("order%3ainjected")]
    public void AggregateIdentity_RejectsUrlEncodedColonsInAggregateId(string maliciousAggId) =>
        // Act & Assert
        Should.Throw<ArgumentException>(
            () => new AggregateIdentity("tenant-a", "orders", maliciousAggId));

    // --- 2.16: NEGATIVE -- reading key for tenantB returns null from tenantA's actor state manager ---

    [Fact]
    public async Task CrossTenantRead_ReturnsNull_WhenQueried_FromDifferentActorStateManager() {
        // Arrange - two separate state managers representing two different actor instances
        var stateManagerA = new InMemoryStateManager();
        var stateManagerB = new InMemoryStateManager();
        _ = new AggregateIdentity("tenant-a", "orders", "order-001");
        var identityB = new AggregateIdentity("tenant-b", "orders", "order-001");

        // Write event to tenantB's state manager
        string tenantBKey = $"{identityB.EventStreamKeyPrefix}1";
        await stateManagerB.SetStateAsync(tenantBKey, "tenantB-event-data");
        await stateManagerB.SaveStateAsync();

        // Act - attempt to read tenantB's key from tenantA's state manager
        ConditionalValue<string> result = await stateManagerA.TryGetStateAsync<string>(tenantBKey);

        // Assert - tenantA's state manager should NOT have tenantB's data
        result.HasValue.ShouldBeFalse();

        // Verify tenantB's state manager DOES have the data
        ConditionalValue<string> tenantBResult = await stateManagerB.TryGetStateAsync<string>(tenantBKey);
        tenantBResult.HasValue.ShouldBeTrue();
        tenantBResult.Value.ShouldBe("tenantB-event-data");
    }

    // --- Review fix H1: Shared state manager test validates Layer 2 (composite key prefixing) independently ---

    [Fact]
    public async Task SharedStateManager_TenantPrefixedKeys_PreventCrossTenantRead() {
        // Arrange - SINGLE shared state manager simulating bypassed actor scoping (Layer 3).
        // This tests Layer 2 isolation (composite key prefixing) independently.
        var sharedStateManager = new InMemoryStateManager();

        var identityA = new AggregateIdentity("tenant-a", "orders", "order-001");
        var identityB = new AggregateIdentity("tenant-b", "orders", "order-001");

        // Write tenant-b's event to shared state store
        string tenantBEventKey = $"{identityB.EventStreamKeyPrefix}1";
        await sharedStateManager.SetStateAsync(tenantBEventKey, "tenantB-event-data");

        // Write tenant-b's metadata to shared state store
        await sharedStateManager.SetStateAsync(identityB.MetadataKey, "tenantB-metadata");

        // Write tenant-b's snapshot to shared state store
        await sharedStateManager.SetStateAsync(identityB.SnapshotKey, "tenantB-snapshot");
        await sharedStateManager.SaveStateAsync();

        // Act - attempt to read using tenant-a's KEY PREFIXES from the SAME state store
        string tenantAEventKey = $"{identityA.EventStreamKeyPrefix}1";
        ConditionalValue<string> eventResult = await sharedStateManager.TryGetStateAsync<string>(tenantAEventKey);
        ConditionalValue<string> metadataResult = await sharedStateManager.TryGetStateAsync<string>(identityA.MetadataKey);
        ConditionalValue<string> snapshotResult = await sharedStateManager.TryGetStateAsync<string>(identityA.SnapshotKey);

        // Assert - tenant-a's keys cannot access tenant-b's data even in a shared state store
        eventResult.HasValue.ShouldBeFalse();
        metadataResult.HasValue.ShouldBeFalse();
        snapshotResult.HasValue.ShouldBeFalse();

        // Verify tenant-b's keys ARE accessible from the same shared store
        ConditionalValue<string> tenantBEvent = await sharedStateManager.TryGetStateAsync<string>(tenantBEventKey);
        tenantBEvent.HasValue.ShouldBeTrue();
        tenantBEvent.Value.ShouldBe("tenantB-event-data");

        ConditionalValue<string> tenantBMeta = await sharedStateManager.TryGetStateAsync<string>(identityB.MetadataKey);
        tenantBMeta.HasValue.ShouldBeTrue();

        ConditionalValue<string> tenantBSnap = await sharedStateManager.TryGetStateAsync<string>(identityB.SnapshotKey);
        tenantBSnap.HasValue.ShouldBeTrue();
    }

    // --- Review fix H2: DEL character (0x7F) rejection test ---

    [Fact]
    public void AggregateIdentity_RejectsDelCharacterInTenant() =>
        // DEL (0x7F) is a control character and must be rejected
        Should.Throw<ArgumentException>(
            () => new AggregateIdentity("tenant\u007F", "orders", "order-001"));

    [Fact]
    public void AggregateIdentity_RejectsDelCharacterInDomain() => Should.Throw<ArgumentException>(
            () => new AggregateIdentity("tenant-a", "orders\u007F", "order-001"));

    [Fact]
    public void AggregateIdentity_RejectsDelCharacterInAggregateId() => Should.Throw<ArgumentException>(
            () => new AggregateIdentity("tenant-a", "orders", "order\u007F001"));
}
