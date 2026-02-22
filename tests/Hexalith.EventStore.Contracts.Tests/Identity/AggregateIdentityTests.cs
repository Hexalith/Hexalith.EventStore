namespace Hexalith.EventStore.Contracts.Tests.Identity;

using Hexalith.EventStore.Contracts.Identity;

public class AggregateIdentityTests {
    [Fact]
    public void Constructor_WithValidInputs_CreatesInstance() {
        var identity = new AggregateIdentity("acme", "payments", "order-123");

        Assert.Equal("acme", identity.TenantId);
        Assert.Equal("payments", identity.Domain);
        Assert.Equal("order-123", identity.AggregateId);
    }

    [Fact]
    public void Constructor_ForcesLowercase_ForTenantIdAndDomain() {
        var identity = new AggregateIdentity("ACME", "Payments", "order-123");

        Assert.Equal("acme", identity.TenantId);
        Assert.Equal("payments", identity.Domain);
        Assert.Equal("order-123", identity.AggregateId);
    }

    [Fact]
    public void ActorId_ReturnsColonSeparatedCanonicalForm() {
        var identity = new AggregateIdentity("acme", "payments", "order-123");

        Assert.Equal("acme:payments:order-123", identity.ActorId);
    }

    [Fact]
    public void EventStreamKeyPrefix_ReturnsCorrectFormat() {
        var identity = new AggregateIdentity("acme", "payments", "order-123");

        Assert.Equal("acme:payments:order-123:events:", identity.EventStreamKeyPrefix);
    }

    [Fact]
    public void MetadataKey_ReturnsCorrectFormat() {
        var identity = new AggregateIdentity("acme", "payments", "order-123");

        Assert.Equal("acme:payments:order-123:metadata", identity.MetadataKey);
    }

    [Fact]
    public void SnapshotKey_ReturnsCorrectFormat() {
        var identity = new AggregateIdentity("acme", "payments", "order-123");

        Assert.Equal("acme:payments:order-123:snapshot", identity.SnapshotKey);
    }

    [Fact]
    public void PubSubTopic_ReturnsDotSeparatedFormat() {
        var identity = new AggregateIdentity("acme", "payments", "order-123");

        Assert.Equal("acme.payments.events", identity.PubSubTopic);
    }

    [Fact]
    public void QueueSession_ReturnsColonSeparatedForm() {
        var identity = new AggregateIdentity("acme", "payments", "order-123");

        Assert.Equal("acme:payments:order-123", identity.QueueSession);
    }

    [Fact]
    public void ToString_ReturnsCanonicalForm() {
        var identity = new AggregateIdentity("acme", "payments", "order-123");

        Assert.Equal("acme:payments:order-123", identity.ToString());
    }

    [Theory]
    [InlineData(null)]
    public void Constructor_WithNullTenantId_ThrowsArgumentNullException(string? tenantId) {
        Assert.Throws<ArgumentNullException>(() => new AggregateIdentity(tenantId!, "payments", "order-123"));
    }

    [Theory]
    [InlineData(null)]
    public void Constructor_WithNullDomain_ThrowsArgumentNullException(string? domain) {
        Assert.Throws<ArgumentNullException>(() => new AggregateIdentity("acme", domain!, "order-123"));
    }

    [Theory]
    [InlineData(null)]
    public void Constructor_WithNullAggregateId_ThrowsArgumentNullException(string? aggregateId) {
        Assert.Throws<ArgumentNullException>(() => new AggregateIdentity("acme", "payments", aggregateId!));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("\t")]
    public void Constructor_WithEmptyOrWhitespaceTenantId_ThrowsArgumentException(string tenantId) {
        Assert.Throws<ArgumentException>(() => new AggregateIdentity(tenantId, "payments", "order-123"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("\t")]
    public void Constructor_WithEmptyOrWhitespaceDomain_ThrowsArgumentException(string domain) {
        Assert.Throws<ArgumentException>(() => new AggregateIdentity("acme", domain, "order-123"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("\t")]
    public void Constructor_WithEmptyOrWhitespaceAggregateId_ThrowsArgumentException(string aggregateId) {
        Assert.Throws<ArgumentException>(() => new AggregateIdentity("acme", "payments", aggregateId));
    }

    [Theory]
    [InlineData("tenant:id")]
    [InlineData("tenant.id")]
    [InlineData("tenant id")]
    [InlineData("-tenant")]
    [InlineData("tenant-")]
    [InlineData("ten\u0001ant")]
    [InlineData("ten\u00e9nt")]
    public void Constructor_WithInvalidTenantId_ThrowsArgumentException(string tenantId) {
        Assert.Throws<ArgumentException>(() => new AggregateIdentity(tenantId, "payments", "order-123"));
    }

    [Theory]
    [InlineData("domain:name")]
    [InlineData("domain.name")]
    [InlineData("domain name")]
    [InlineData("-domain")]
    [InlineData("domain-")]
    [InlineData("dom\u0001ain")]
    [InlineData("dom\u00e9in")]
    public void Constructor_WithInvalidDomain_ThrowsArgumentException(string domain) {
        Assert.Throws<ArgumentException>(() => new AggregateIdentity("acme", domain, "order-123"));
    }

    [Theory]
    [InlineData("aggregate:id")]
    [InlineData("aggregate id")]
    [InlineData("-aggregate")]
    [InlineData("aggregate-")]
    [InlineData(".aggregate")]
    [InlineData("aggregate.")]
    [InlineData("agg\u0001regate")]
    [InlineData("agg\u00e9regate")]
    public void Constructor_WithInvalidAggregateId_ThrowsArgumentException(string aggregateId) {
        Assert.Throws<ArgumentException>(() => new AggregateIdentity("acme", "payments", aggregateId));
    }

    [Fact]
    public void Constructor_WithTenantIdExceeding64Chars_ThrowsArgumentException() {
        string longTenantId = new('a', 65);
        Assert.Throws<ArgumentException>(() => new AggregateIdentity(longTenantId, "payments", "order-123"));
    }

    [Fact]
    public void Constructor_WithDomainExceeding64Chars_ThrowsArgumentException() {
        string longDomain = new('a', 65);
        Assert.Throws<ArgumentException>(() => new AggregateIdentity("acme", longDomain, "order-123"));
    }

    [Fact]
    public void Constructor_WithAggregateIdExceeding256Chars_ThrowsArgumentException() {
        string longAggregateId = new('a', 257);
        Assert.Throws<ArgumentException>(() => new AggregateIdentity("acme", "payments", longAggregateId));
    }

    [Fact]
    public void Constructor_WithMaxLengthTenantId_Succeeds() {
        string tenantId = new('a', 64);
        var identity = new AggregateIdentity(tenantId, "payments", "order-123");
        Assert.Equal(tenantId, identity.TenantId);
    }

    [Fact]
    public void Constructor_WithMaxLengthDomain_Succeeds() {
        string domain = new('a', 64);
        var identity = new AggregateIdentity("acme", domain, "order-123");
        Assert.Equal(domain, identity.Domain);
    }

    [Fact]
    public void Constructor_WithMaxLengthAggregateId_Succeeds() {
        string aggregateId = new('a', 256);
        var identity = new AggregateIdentity("acme", "payments", aggregateId);
        Assert.Equal(aggregateId, identity.AggregateId);
    }

    [Fact]
    public void Constructor_WithSingleCharComponents_Succeeds() {
        var identity = new AggregateIdentity("a", "b", "c");

        Assert.Equal("a", identity.TenantId);
        Assert.Equal("b", identity.Domain);
        Assert.Equal("c", identity.AggregateId);
    }

    [Theory]
    [InlineData("acme", "payments", "order-123")]
    [InlineData("tenant1", "domain1", "agg.123")]
    [InlineData("a", "b", "c")]
    [InlineData("tenant-1", "domain-2", "Order_123.v2")]
    public void RecordEquality_SameValues_AreEqual(string tenantId, string domain, string aggregateId) {
        var identity1 = new AggregateIdentity(tenantId, domain, aggregateId);
        var identity2 = new AggregateIdentity(tenantId, domain, aggregateId);

        Assert.Equal(identity1, identity2);
    }

    [Fact]
    public void AggregateId_AllowsDotsAndUnderscores() {
        var identity = new AggregateIdentity("acme", "payments", "order_123.v2");

        Assert.Equal("order_123.v2", identity.AggregateId);
    }

    [Fact]
    public void AggregateId_IsCaseSensitive() {
        var identity = new AggregateIdentity("acme", "payments", "Order-123");

        Assert.Equal("Order-123", identity.AggregateId);
    }

    // --- Task 2: Multi-tenant actor isolation verification ---

    [Fact]
    public void ActorId_DifferentTenantsSameDomainAndAggregate_ProducesDistinctActorIds() {
        var identityA = new AggregateIdentity("tenant-a", "orders", "order-001");
        var identityB = new AggregateIdentity("tenant-b", "orders", "order-001");

        Assert.NotEqual(identityA.ActorId, identityB.ActorId);
        Assert.Equal("tenant-a:orders:order-001", identityA.ActorId);
        Assert.Equal("tenant-b:orders:order-001", identityB.ActorId);
    }

    [Fact]
    public void ActorId_SameTenantDifferentDomains_ProducesDistinctActorIds() {
        var identityOrders = new AggregateIdentity("tenant-a", "orders", "item-001");
        var identityInventory = new AggregateIdentity("tenant-a", "inventory", "item-001");

        Assert.NotEqual(identityOrders.ActorId, identityInventory.ActorId);
        Assert.Equal("tenant-a:orders:item-001", identityOrders.ActorId);
        Assert.Equal("tenant-a:inventory:item-001", identityInventory.ActorId);
    }

    [Fact]
    public void AllKeys_DifferentTenants_AreStructurallyDisjoint() {
        var identityA = new AggregateIdentity("tenant-a", "orders", "order-001");
        var identityB = new AggregateIdentity("tenant-b", "orders", "order-001");

        // Actor IDs are distinct
        Assert.NotEqual(identityA.ActorId, identityB.ActorId);

        // Event stream key prefixes are distinct
        Assert.NotEqual(identityA.EventStreamKeyPrefix, identityB.EventStreamKeyPrefix);

        // Metadata keys are distinct
        Assert.NotEqual(identityA.MetadataKey, identityB.MetadataKey);

        // Snapshot keys are distinct
        Assert.NotEqual(identityA.SnapshotKey, identityB.SnapshotKey);

        // No key from tenant A starts with tenant B's prefix
        Assert.DoesNotContain("tenant-b", identityA.EventStreamKeyPrefix);
        Assert.DoesNotContain("tenant-a", identityB.EventStreamKeyPrefix);
    }

    // --- Task 3: Composite key isolation in state store ---

    [Fact]
    public void EventStreamKeyPrefix_IncludesTenantPrefix() {
        // AC #2: Event keys include tenant prefix: {tenant}:{domain}:{aggId}:events:{seq}
        var identity = new AggregateIdentity("tenant-a", "orders", "order-001");
        string eventKey = identity.EventStreamKeyPrefix + "42";

        Assert.Equal("tenant-a:orders:order-001:events:42", eventKey);
        Assert.StartsWith("tenant-a:", eventKey);
    }

    [Fact]
    public void SnapshotKey_IncludesTenantPrefix() {
        // Task 3.2: snapshot keys include tenant prefix: {tenant}:{domain}:{aggId}:snapshot
        var identity = new AggregateIdentity("tenant-a", "orders", "order-001");

        Assert.Equal("tenant-a:orders:order-001:snapshot", identity.SnapshotKey);
        Assert.StartsWith("tenant-a:", identity.SnapshotKey);
    }

    [Fact]
    public void MetadataKey_IncludesTenantPrefix() {
        // Task 3.3: metadata keys follow {tenant}:{domain}:{aggId}:metadata pattern
        var identity = new AggregateIdentity("tenant-a", "orders", "order-001");

        Assert.Equal("tenant-a:orders:order-001:metadata", identity.MetadataKey);
        Assert.StartsWith("tenant-a:", identity.MetadataKey);
    }

    [Fact]
    public void PipelineKeyPrefix_ReturnsCorrectFormat() {
        var identity = new AggregateIdentity("acme", "payments", "order-123");

        Assert.Equal("acme:payments:order-123:pipeline:", identity.PipelineKeyPrefix);
    }

    [Fact]
    public void PipelineKeyPrefix_IncludesTenantPrefix() {
        var identity = new AggregateIdentity("tenant-a", "orders", "order-001");
        string pipelineKey = identity.PipelineKeyPrefix + "corr-123";

        Assert.Equal("tenant-a:orders:order-001:pipeline:corr-123", pipelineKey);
        Assert.StartsWith("tenant-a:", pipelineKey);
    }

    [Fact]
    public void PipelineKeyPrefix_DifferentTenants_AreDisjoint() {
        var identityA = new AggregateIdentity("tenant-a", "orders", "order-001");
        var identityB = new AggregateIdentity("tenant-b", "orders", "order-001");

        Assert.NotEqual(identityA.PipelineKeyPrefix, identityB.PipelineKeyPrefix);
        Assert.StartsWith("tenant-a:", identityA.PipelineKeyPrefix);
        Assert.StartsWith("tenant-b:", identityB.PipelineKeyPrefix);
    }

    [Fact]
    public void AllStateStoreKeys_TenantAAndTenantB_NoOverlap() {
        // Task 3.4: tenant A's keys are structurally disjoint from tenant B's keys
        var identityA = new AggregateIdentity("tenant-a", "orders", "order-001");
        var identityB = new AggregateIdentity("tenant-b", "orders", "order-001");

        // Collect all key patterns for both tenants
        string[] keysA =
        [
            identityA.EventStreamKeyPrefix + "1",
            identityA.SnapshotKey,
            identityA.MetadataKey,
            identityA.PipelineKeyPrefix + "corr-1",
        ];

        string[] keysB =
        [
            identityB.EventStreamKeyPrefix + "1",
            identityB.SnapshotKey,
            identityB.MetadataKey,
            identityB.PipelineKeyPrefix + "corr-1",
        ];

        // No overlap: no key from A equals any key from B
        foreach (string keyA in keysA) {
            foreach (string keyB in keysB) {
                Assert.NotEqual(keyA, keyB);
            }
        }

        // All keys from A start with "tenant-a:" and none start with "tenant-b:"
        foreach (string keyA in keysA) {
            Assert.StartsWith("tenant-a:", keyA);
            Assert.False(keyA.StartsWith("tenant-b:"));
        }

        // All keys from B start with "tenant-b:" and none start with "tenant-a:"
        foreach (string keyB in keysB) {
            Assert.StartsWith("tenant-b:", keyB);
            Assert.False(keyB.StartsWith("tenant-a:"));
        }
    }
}
