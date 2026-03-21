using Hexalith.EventStore.Admin.Abstractions.Models.Tenants;

namespace Hexalith.EventStore.Admin.Abstractions.Tests.Models.Tenants;

public class TenantComparisonTests
{
    [Fact]
    public void Constructor_WithValidInputs_CreatesInstance()
    {
        var tenants = new List<TenantSummary>
        {
            new("acme", "Acme Corp", TenantStatusType.Active, 1000, 5),
            new("contoso", "Contoso", TenantStatusType.Active, 500, 3),
        };
        var comparison = new TenantComparison(tenants, DateTimeOffset.UtcNow);

        comparison.Tenants.Count.ShouldBe(2);
    }

    [Fact]
    public void Constructor_WithNullTenants_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() =>
            new TenantComparison(null!, DateTimeOffset.UtcNow));
    }
}
