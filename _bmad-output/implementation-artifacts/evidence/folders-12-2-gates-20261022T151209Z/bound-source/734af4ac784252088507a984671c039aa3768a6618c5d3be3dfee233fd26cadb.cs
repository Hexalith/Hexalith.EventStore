using Hexalith.EventStore.Admin.Abstractions.Models.Streams;

namespace Hexalith.EventStore.Admin.Abstractions.Tests.Models.Streams;

public class StreamSummaryTests {
    [Fact]
    public void Constructor_WithValidInputs_CreatesInstance() {
        var summary = new StreamSummary("acme", "orders", "order-123", 10, DateTimeOffset.UtcNow, 10, true, StreamStatus.Active);

        summary.TenantId.ShouldBe("acme");
        summary.Domain.ShouldBe("orders");
        summary.AggregateId.ShouldBe("order-123");
        summary.LastEventSequence.ShouldBe(10);
        summary.EventCount.ShouldBe(10);
        summary.HasSnapshot.ShouldBeTrue();
        summary.StreamStatus.ShouldBe(StreamStatus.Active);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidTenantId_ThrowsArgumentException(string? tenantId) => Should.Throw<ArgumentException>(() =>
                                                                                                      new StreamSummary(tenantId!, "orders", "order-123", 10, DateTimeOffset.UtcNow, 10, false, StreamStatus.Active));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidDomain_ThrowsArgumentException(string? domain) => Should.Throw<ArgumentException>(() =>
                                                                                                  new StreamSummary("acme", domain!, "order-123", 10, DateTimeOffset.UtcNow, 10, false, StreamStatus.Active));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidAggregateId_ThrowsArgumentException(string? aggregateId) => Should.Throw<ArgumentException>(() =>
                                                                                                            new StreamSummary("acme", "orders", aggregateId!, 10, DateTimeOffset.UtcNow, 10, false, StreamStatus.Active));
}
