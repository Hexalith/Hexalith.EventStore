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
    }

    [Fact]
    public void Constructor_WithNullTenantBreakdown_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() =>
            new StorageOverview(0, null, null!));
    }
}
