using Hexalith.EventStore.Admin.Abstractions.Models.Storage;

namespace Hexalith.EventStore.Admin.Abstractions.Tests.Models.Storage;

public class StreamStorageInfoTests {
    [Fact]
    public void Constructor_WithValidInputs_CreatesInstance() {
        var info = new StreamStorageInfo("acme", "orders", "order-123", "Order", 100, 2048, true, TimeSpan.FromHours(1));

        info.TenantId.ShouldBe("acme");
        info.Domain.ShouldBe("orders");
        info.AggregateId.ShouldBe("order-123");
        info.AggregateType.ShouldBe("Order");
        info.EventCount.ShouldBe(100);
        info.SizeBytes.ShouldBe(2048);
        info.HasSnapshot.ShouldBeTrue();
        info.SnapshotAge.ShouldBe(TimeSpan.FromHours(1));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidTenantId_ThrowsArgumentException(string? tenantId) => Should.Throw<ArgumentException>(() =>
                                                                                                      new StreamStorageInfo(tenantId!, "orders", "order-123", "Order", 0, null, false, null));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidDomain_ThrowsArgumentException(string? domain) => Should.Throw<ArgumentException>(() =>
                                                                                                  new StreamStorageInfo("acme", domain!, "order-123", "Order", 0, null, false, null));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidAggregateId_ThrowsArgumentException(string? aggregateId) => Should.Throw<ArgumentException>(() =>
                                                                                                            new StreamStorageInfo("acme", "orders", aggregateId!, "Order", 0, null, false, null));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidAggregateType_ThrowsArgumentException(string? aggregateType) => Should.Throw<ArgumentException>(() =>
                                                                                                                new StreamStorageInfo("acme", "orders", "order-123", aggregateType!, 0, null, false, null));
}
