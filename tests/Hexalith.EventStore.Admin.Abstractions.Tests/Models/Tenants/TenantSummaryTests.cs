using Hexalith.EventStore.Admin.Abstractions.Models.Tenants;

namespace Hexalith.EventStore.Admin.Abstractions.Tests.Models.Tenants;

public class TenantSummaryTests
{
    [Fact]
    public void Constructor_WithValidInputs_CreatesInstance()
    {
        var summary = new TenantSummary("acme", "Acme Corp", TenantStatusType.Active, 1000, 5);

        summary.TenantId.ShouldBe("acme");
        summary.DisplayName.ShouldBe("Acme Corp");
        summary.Status.ShouldBe(TenantStatusType.Active);
        summary.EventCount.ShouldBe(1000);
        summary.DomainCount.ShouldBe(5);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidTenantId_ThrowsArgumentException(string? tenantId)
    {
        Should.Throw<ArgumentException>(() =>
            new TenantSummary(tenantId!, "Acme Corp", TenantStatusType.Active, 0, 0));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidDisplayName_ThrowsArgumentException(string? displayName)
    {
        Should.Throw<ArgumentException>(() =>
            new TenantSummary("acme", displayName!, TenantStatusType.Active, 0, 0));
    }
}
