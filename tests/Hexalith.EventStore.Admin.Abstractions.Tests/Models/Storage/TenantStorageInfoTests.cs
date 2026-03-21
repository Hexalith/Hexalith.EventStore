using Hexalith.EventStore.Admin.Abstractions.Models.Storage;

namespace Hexalith.EventStore.Admin.Abstractions.Tests.Models.Storage;

public class TenantStorageInfoTests
{
    [Fact]
    public void Constructor_WithValidInputs_CreatesInstance()
    {
        var info = new TenantStorageInfo("acme", 500, 1024, 10.5);

        info.TenantId.ShouldBe("acme");
        info.EventCount.ShouldBe(500);
        info.SizeBytes.ShouldBe(1024);
        info.GrowthRatePerDay.ShouldBe(10.5);
    }

    [Fact]
    public void Constructor_WithNullOptionalFields_CreatesInstance()
    {
        var info = new TenantStorageInfo("acme", 500, null, null);

        info.SizeBytes.ShouldBeNull();
        info.GrowthRatePerDay.ShouldBeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidTenantId_ThrowsArgumentException(string? tenantId)
    {
        Should.Throw<ArgumentException>(() =>
            new TenantStorageInfo(tenantId!, 0, null, null));
    }

    [Fact]
    public void Constructor_WithNaNGrowthRate_ThrowsArgumentOutOfRangeException()
    {
        Should.Throw<ArgumentOutOfRangeException>(() =>
            new TenantStorageInfo("acme", 0, null, double.NaN));
    }

    [Fact]
    public void Constructor_WithInfinityGrowthRate_ThrowsArgumentOutOfRangeException()
    {
        Should.Throw<ArgumentOutOfRangeException>(() =>
            new TenantStorageInfo("acme", 0, null, double.PositiveInfinity));
    }
}
