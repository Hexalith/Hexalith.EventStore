using Hexalith.EventStore.Admin.Abstractions.Models.Projections;

namespace Hexalith.EventStore.Admin.Abstractions.Tests.Models.Projections;

public class ProjectionStatusTests {
    [Fact]
    public void Constructor_WithValidInputs_CreatesInstance() {
        var status = new ProjectionStatus("OrderSummary", "acme", ProjectionStatusType.Running, 5, 100.5, 0, 1000, DateTimeOffset.UtcNow);

        status.Name.ShouldBe("OrderSummary");
        status.TenantId.ShouldBe("acme");
        status.Status.ShouldBe(ProjectionStatusType.Running);
        status.Lag.ShouldBe(5);
        status.Throughput.ShouldBe(100.5);
        status.ErrorCount.ShouldBe(0);
        status.LastProcessedPosition.ShouldBe(1000);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidName_ThrowsArgumentException(string? name) => Should.Throw<ArgumentException>(() =>
                                                                                              new ProjectionStatus(name!, "acme", ProjectionStatusType.Running, 0, 0, 0, 0, DateTimeOffset.UtcNow));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidTenantId_ThrowsArgumentException(string? tenantId) => Should.Throw<ArgumentException>(() =>
                                                                                                      new ProjectionStatus("OrderSummary", tenantId!, ProjectionStatusType.Running, 0, 0, 0, 0, DateTimeOffset.UtcNow));
}
