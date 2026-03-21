using Hexalith.EventStore.Admin.Abstractions.Models.Tenants;

namespace Hexalith.EventStore.Admin.Abstractions.Tests.Models.Tenants;

public class TenantQuotasTests
{
    [Fact]
    public void Constructor_WithValidInputs_CreatesInstance()
    {
        var quotas = new TenantQuotas("acme", 10000, 1073741824, 536870912);

        quotas.TenantId.ShouldBe("acme");
        quotas.MaxEventsPerDay.ShouldBe(10000);
        quotas.MaxStorageBytes.ShouldBe(1073741824);
        quotas.CurrentUsage.ShouldBe(536870912);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidTenantId_ThrowsArgumentException(string? tenantId)
    {
        Should.Throw<ArgumentException>(() =>
            new TenantQuotas(tenantId!, 10000, 1073741824, 0));
    }
}
