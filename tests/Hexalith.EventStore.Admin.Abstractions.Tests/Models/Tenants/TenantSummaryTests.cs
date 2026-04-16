using Hexalith.EventStore.Admin.Abstractions.Models.Tenants;

namespace Hexalith.EventStore.Admin.Abstractions.Tests.Models.Tenants;

public class TenantSummaryTests {
    [Fact]
    public void Constructor_WithValidInputs_CreatesInstance() {
        var summary = new TenantSummary("acme", "Acme Corp", TenantStatusType.Active);

        summary.TenantId.ShouldBe("acme");
        summary.Name.ShouldBe("Acme Corp");
        summary.Status.ShouldBe(TenantStatusType.Active);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidTenantId_ThrowsArgumentException(string? tenantId) => Should.Throw<ArgumentException>(() =>
                                                                                                      new TenantSummary(tenantId!, "Acme Corp", TenantStatusType.Active));

}
