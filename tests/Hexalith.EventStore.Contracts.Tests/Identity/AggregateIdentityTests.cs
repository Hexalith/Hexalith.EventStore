namespace Hexalith.EventStore.Contracts.Tests.Identity;

using Hexalith.EventStore.Contracts.Identity;

public class AggregateIdentityTests
{
    [Fact]
    public void Constructor_WithValidInputs_CreatesInstance()
    {
        var identity = new AggregateIdentity("acme", "payments", "order-123");

        Assert.Equal("acme", identity.TenantId);
        Assert.Equal("payments", identity.Domain);
        Assert.Equal("order-123", identity.AggregateId);
    }

    [Fact]
    public void Constructor_ForcesLowercase_ForTenantIdAndDomain()
    {
        var identity = new AggregateIdentity("ACME", "Payments", "order-123");

        Assert.Equal("acme", identity.TenantId);
        Assert.Equal("payments", identity.Domain);
        Assert.Equal("order-123", identity.AggregateId);
    }

    [Fact]
    public void ActorId_ReturnsColonSeparatedCanonicalForm()
    {
        var identity = new AggregateIdentity("acme", "payments", "order-123");

        Assert.Equal("acme:payments:order-123", identity.ActorId);
    }

    [Fact]
    public void EventStreamKeyPrefix_ReturnsCorrectFormat()
    {
        var identity = new AggregateIdentity("acme", "payments", "order-123");

        Assert.Equal("acme:payments:order-123:events:", identity.EventStreamKeyPrefix);
    }

    [Fact]
    public void MetadataKey_ReturnsCorrectFormat()
    {
        var identity = new AggregateIdentity("acme", "payments", "order-123");

        Assert.Equal("acme:payments:order-123:metadata", identity.MetadataKey);
    }

    [Fact]
    public void SnapshotKey_ReturnsCorrectFormat()
    {
        var identity = new AggregateIdentity("acme", "payments", "order-123");

        Assert.Equal("acme:payments:order-123:snapshot", identity.SnapshotKey);
    }

    [Fact]
    public void PubSubTopic_ReturnsDotSeparatedFormat()
    {
        var identity = new AggregateIdentity("acme", "payments", "order-123");

        Assert.Equal("acme.payments.events", identity.PubSubTopic);
    }

    [Fact]
    public void QueueSession_ReturnsColonSeparatedForm()
    {
        var identity = new AggregateIdentity("acme", "payments", "order-123");

        Assert.Equal("acme:payments:order-123", identity.QueueSession);
    }

    [Fact]
    public void ToString_ReturnsCanonicalForm()
    {
        var identity = new AggregateIdentity("acme", "payments", "order-123");

        Assert.Equal("acme:payments:order-123", identity.ToString());
    }

    [Theory]
    [InlineData(null)]
    public void Constructor_WithNullTenantId_ThrowsArgumentNullException(string? tenantId)
    {
        Assert.Throws<ArgumentNullException>(() => new AggregateIdentity(tenantId!, "payments", "order-123"));
    }

    [Theory]
    [InlineData(null)]
    public void Constructor_WithNullDomain_ThrowsArgumentNullException(string? domain)
    {
        Assert.Throws<ArgumentNullException>(() => new AggregateIdentity("acme", domain!, "order-123"));
    }

    [Theory]
    [InlineData(null)]
    public void Constructor_WithNullAggregateId_ThrowsArgumentNullException(string? aggregateId)
    {
        Assert.Throws<ArgumentNullException>(() => new AggregateIdentity("acme", "payments", aggregateId!));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("\t")]
    public void Constructor_WithEmptyOrWhitespaceTenantId_ThrowsArgumentException(string tenantId)
    {
        Assert.Throws<ArgumentException>(() => new AggregateIdentity(tenantId, "payments", "order-123"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("\t")]
    public void Constructor_WithEmptyOrWhitespaceDomain_ThrowsArgumentException(string domain)
    {
        Assert.Throws<ArgumentException>(() => new AggregateIdentity("acme", domain, "order-123"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("\t")]
    public void Constructor_WithEmptyOrWhitespaceAggregateId_ThrowsArgumentException(string aggregateId)
    {
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
    public void Constructor_WithInvalidTenantId_ThrowsArgumentException(string tenantId)
    {
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
    public void Constructor_WithInvalidDomain_ThrowsArgumentException(string domain)
    {
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
    public void Constructor_WithInvalidAggregateId_ThrowsArgumentException(string aggregateId)
    {
        Assert.Throws<ArgumentException>(() => new AggregateIdentity("acme", "payments", aggregateId));
    }

    [Fact]
    public void Constructor_WithTenantIdExceeding64Chars_ThrowsArgumentException()
    {
        string longTenantId = new('a', 65);
        Assert.Throws<ArgumentException>(() => new AggregateIdentity(longTenantId, "payments", "order-123"));
    }

    [Fact]
    public void Constructor_WithDomainExceeding64Chars_ThrowsArgumentException()
    {
        string longDomain = new('a', 65);
        Assert.Throws<ArgumentException>(() => new AggregateIdentity("acme", longDomain, "order-123"));
    }

    [Fact]
    public void Constructor_WithAggregateIdExceeding256Chars_ThrowsArgumentException()
    {
        string longAggregateId = new('a', 257);
        Assert.Throws<ArgumentException>(() => new AggregateIdentity("acme", "payments", longAggregateId));
    }

    [Fact]
    public void Constructor_WithMaxLengthTenantId_Succeeds()
    {
        string tenantId = new('a', 64);
        var identity = new AggregateIdentity(tenantId, "payments", "order-123");
        Assert.Equal(tenantId, identity.TenantId);
    }

    [Fact]
    public void Constructor_WithMaxLengthDomain_Succeeds()
    {
        string domain = new('a', 64);
        var identity = new AggregateIdentity("acme", domain, "order-123");
        Assert.Equal(domain, identity.Domain);
    }

    [Fact]
    public void Constructor_WithMaxLengthAggregateId_Succeeds()
    {
        string aggregateId = new('a', 256);
        var identity = new AggregateIdentity("acme", "payments", aggregateId);
        Assert.Equal(aggregateId, identity.AggregateId);
    }

    [Fact]
    public void Constructor_WithSingleCharComponents_Succeeds()
    {
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
    public void RecordEquality_SameValues_AreEqual(string tenantId, string domain, string aggregateId)
    {
        var identity1 = new AggregateIdentity(tenantId, domain, aggregateId);
        var identity2 = new AggregateIdentity(tenantId, domain, aggregateId);

        Assert.Equal(identity1, identity2);
    }

    [Fact]
    public void AggregateId_AllowsDotsAndUnderscores()
    {
        var identity = new AggregateIdentity("acme", "payments", "order_123.v2");

        Assert.Equal("order_123.v2", identity.AggregateId);
    }

    [Fact]
    public void AggregateId_IsCaseSensitive()
    {
        var identity = new AggregateIdentity("acme", "payments", "Order-123");

        Assert.Equal("Order-123", identity.AggregateId);
    }
}
