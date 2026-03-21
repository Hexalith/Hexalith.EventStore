using Hexalith.EventStore.Admin.Abstractions.Models.Streams;

namespace Hexalith.EventStore.Admin.Abstractions.Tests.Models.Streams;

public class AggregateStateSnapshotTests
{
    [Fact]
    public void Constructor_WithValidInputs_CreatesInstance()
    {
        var snapshot = new AggregateStateSnapshot("acme", "orders", "order-123", 5, DateTimeOffset.UtcNow, "{\"status\":\"open\"}");

        snapshot.TenantId.ShouldBe("acme");
        snapshot.Domain.ShouldBe("orders");
        snapshot.AggregateId.ShouldBe("order-123");
        snapshot.SequenceNumber.ShouldBe(5);
        snapshot.StateJson.ShouldBe("{\"status\":\"open\"}");
    }

    [Fact]
    public void Constructor_WithNullStateJson_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() =>
            new AggregateStateSnapshot("acme", "orders", "order-123", 5, DateTimeOffset.UtcNow, null!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidTenantId_ThrowsArgumentException(string? tenantId)
    {
        Should.Throw<ArgumentException>(() =>
            new AggregateStateSnapshot(tenantId!, "orders", "order-123", 5, DateTimeOffset.UtcNow, "{}"));
    }

    [Fact]
    public void ToString_RedactsStateJson()
    {
        var snapshot = new AggregateStateSnapshot("acme", "orders", "order-123", 5, DateTimeOffset.UtcNow, "{\"sensitive\":\"data\"}");

        string result = snapshot.ToString();

        result.ShouldContain("[REDACTED]");
        result.ShouldNotContain("sensitive");
        result.ShouldNotContain("{\"sensitive\":\"data\"}");
    }
}
