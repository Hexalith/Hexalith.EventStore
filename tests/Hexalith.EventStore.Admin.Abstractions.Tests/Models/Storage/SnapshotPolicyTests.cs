using Hexalith.EventStore.Admin.Abstractions.Models.Storage;

namespace Hexalith.EventStore.Admin.Abstractions.Tests.Models.Storage;

public class SnapshotPolicyTests {
    [Fact]
    public void Constructor_WithValidInputs_CreatesInstance() {
        var policy = new SnapshotPolicy("acme", "orders", "Order", 100, DateTimeOffset.UtcNow);

        policy.TenantId.ShouldBe("acme");
        policy.Domain.ShouldBe("orders");
        policy.AggregateType.ShouldBe("Order");
        policy.IntervalEvents.ShouldBe(100);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidTenantId_ThrowsArgumentException(string? tenantId) => Should.Throw<ArgumentException>(() =>
                                                                                                      new SnapshotPolicy(tenantId!, "orders", "Order", 100, DateTimeOffset.UtcNow));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidDomain_ThrowsArgumentException(string? domain) => Should.Throw<ArgumentException>(() =>
                                                                                                  new SnapshotPolicy("acme", domain!, "Order", 100, DateTimeOffset.UtcNow));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidAggregateType_ThrowsArgumentException(string? aggregateType) => Should.Throw<ArgumentException>(() =>
                                                                                                                new SnapshotPolicy("acme", "orders", aggregateType!, 100, DateTimeOffset.UtcNow));
}
