using Hexalith.EventStore.Admin.Abstractions.Models.Storage;

namespace Hexalith.EventStore.Admin.Abstractions.Tests.Models.Storage;

public class StorageOverviewTests
{
    [Fact]
    public void Constructor_WithValidInputs_CreatesInstance()
    {
        var overview = new StorageOverview(1000, 1024 * 1024, []);

        overview.TotalEventCount.ShouldBe(1000);
        overview.TotalSizeBytes.ShouldBe(1024 * 1024);
        overview.TenantBreakdown.ShouldBeEmpty();
    }

    [Fact]
    public void Constructor_WithNullSizeBytes_CreatesInstance()
    {
        var overview = new StorageOverview(1000, null, []);

        overview.TotalSizeBytes.ShouldBeNull();
        overview.TotalStreamCount.ShouldBeNull();
    }

    [Fact]
    public void Constructor_WithTotalStreamCount_CreatesInstance()
    {
        var overview = new StorageOverview(1000, null, [], 42);

        overview.TotalStreamCount.ShouldBe(42);
    }

    [Fact]
    public void Constructor_WithNullTenantBreakdown_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() =>
            new StorageOverview(0, null, null!));
    }

    [Fact]
    public void Constructor_WithNegativeTotalStreamCount_ThrowsArgumentOutOfRangeException()
    {
        Should.Throw<ArgumentOutOfRangeException>(() =>
            new StorageOverview(0, null, [], -1));
    }
}
