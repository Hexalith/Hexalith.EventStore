using Hexalith.EventStore.Admin.Abstractions.Models.Streams;

namespace Hexalith.EventStore.Admin.Abstractions.Tests.Models.Streams;

public class EventDetailTests
{
    [Fact]
    public void Constructor_WithValidInputs_CreatesInstance()
    {
        var detail = new EventDetail("acme", "orders", "order-123", 1, "OrderCreated", DateTimeOffset.UtcNow, "corr-001", "cause-001", "user-1", "{\"amount\":100}");

        detail.TenantId.ShouldBe("acme");
        detail.Domain.ShouldBe("orders");
        detail.AggregateId.ShouldBe("order-123");
        detail.SequenceNumber.ShouldBe(1);
        detail.EventTypeName.ShouldBe("OrderCreated");
        detail.CorrelationId.ShouldBe("corr-001");
        detail.CausationId.ShouldBe("cause-001");
        detail.UserId.ShouldBe("user-1");
        detail.PayloadJson.ShouldBe("{\"amount\":100}");
    }

    [Fact]
    public void ToString_RedactsPayloadJson()
    {
        var detail = new EventDetail("acme", "orders", "order-123", 1, "OrderCreated", DateTimeOffset.UtcNow, "corr-001", null, null, "{\"secret\":\"value\"}");

        string result = detail.ToString();

        result.ShouldContain("[REDACTED]");
        result.ShouldNotContain("secret");
        result.ShouldNotContain("{\"secret\":\"value\"}");
    }

    [Fact]
    public void Constructor_WithNullPayloadJson_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() =>
            new EventDetail("acme", "orders", "order-123", 1, "OrderCreated", DateTimeOffset.UtcNow, "corr-001", null, null, null!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidTenantId_ThrowsArgumentException(string? tenantId)
    {
        Should.Throw<ArgumentException>(() =>
            new EventDetail(tenantId!, "orders", "order-123", 1, "OrderCreated", DateTimeOffset.UtcNow, "corr-001", null, null, "{}"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidDomain_ThrowsArgumentException(string? domain)
    {
        Should.Throw<ArgumentException>(() =>
            new EventDetail("acme", domain!, "order-123", 1, "OrderCreated", DateTimeOffset.UtcNow, "corr-001", null, null, "{}"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidAggregateId_ThrowsArgumentException(string? aggregateId)
    {
        Should.Throw<ArgumentException>(() =>
            new EventDetail("acme", "orders", aggregateId!, 1, "OrderCreated", DateTimeOffset.UtcNow, "corr-001", null, null, "{}"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidEventTypeName_ThrowsArgumentException(string? eventTypeName)
    {
        Should.Throw<ArgumentException>(() =>
            new EventDetail("acme", "orders", "order-123", 1, eventTypeName!, DateTimeOffset.UtcNow, "corr-001", null, null, "{}"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidCorrelationId_ThrowsArgumentException(string? correlationId)
    {
        Should.Throw<ArgumentException>(() =>
            new EventDetail("acme", "orders", "order-123", 1, "OrderCreated", DateTimeOffset.UtcNow, correlationId!, null, null, "{}"));
    }
}
