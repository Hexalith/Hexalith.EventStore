using Hexalith.EventStore.Admin.Abstractions.Models.Tenants;
using Hexalith.EventStore.Admin.Abstractions.Models.TypeCatalog;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

namespace Hexalith.EventStore.Admin.UI.Tests.Services;

public class TopologyCacheServiceTests
{
    private static AdminStreamApiClient CreateMockApiClient()
    {
        return Substitute.For<AdminStreamApiClient>(
            Substitute.For<IHttpClientFactory>(),
            NullLogger<AdminStreamApiClient>.Instance);
    }

    [Fact]
    public async Task EnsureLoadedAsync_LoadsTenants_OnFirstCall()
    {
        AdminStreamApiClient apiClient = CreateMockApiClient();
        List<TenantSummary> tenants = [new("t1", "Tenant One", TenantStatusType.Active, 100, 2)];
        List<AggregateTypeInfo> types = [new("Counter", "Counting", 3, 2, true)];
        apiClient.GetTenantsAsync(Arg.Any<CancellationToken>()).Returns(tenants);
        apiClient.GetAggregateTypesAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(types);

        TopologyCacheService sut = new(apiClient);

        await sut.EnsureLoadedAsync();

        sut.IsLoaded.ShouldBeTrue();
        sut.Tenants.Count.ShouldBe(1);
        sut.Tenants[0].TenantId.ShouldBe("t1");
        sut.Domains.Count.ShouldBe(1);
        sut.Domains[0].ShouldBe("Counting");
    }

    [Fact]
    public async Task EnsureLoadedAsync_DoesNotReload_OnSecondCall()
    {
        AdminStreamApiClient apiClient = CreateMockApiClient();
        List<TenantSummary> tenants = [new("t1", "Tenant One", TenantStatusType.Active, 100, 2)];
        List<AggregateTypeInfo> types = [new("Counter", "Counting", 3, 2, true)];
        apiClient.GetTenantsAsync(Arg.Any<CancellationToken>()).Returns(tenants);
        apiClient.GetAggregateTypesAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(types);

        TopologyCacheService sut = new(apiClient);

        await sut.EnsureLoadedAsync();
        await sut.EnsureLoadedAsync();

        await apiClient.Received(1).GetTenantsAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefreshAsync_ReloadsData()
    {
        AdminStreamApiClient apiClient = CreateMockApiClient();
        List<TenantSummary> tenants1 = [new("t1", "Tenant One", TenantStatusType.Active, 100, 2)];
        List<TenantSummary> tenants2 = [new("t1", "Tenant One", TenantStatusType.Active, 100, 2), new("t2", "Tenant Two", TenantStatusType.Active, 50, 1)];
        List<AggregateTypeInfo> types = [new("Counter", "Counting", 3, 2, true)];
        apiClient.GetTenantsAsync(Arg.Any<CancellationToken>()).Returns(tenants1, tenants2);
        apiClient.GetAggregateTypesAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(types);

        TopologyCacheService sut = new(apiClient);

        await sut.EnsureLoadedAsync();
        sut.Tenants.Count.ShouldBe(1);

        await sut.RefreshAsync();
        sut.Tenants.Count.ShouldBe(2);
    }

    [Fact]
    public async Task RefreshAsync_KeepsStaleTenants_WhenTenantLoadTimesOut()
    {
        AdminStreamApiClient apiClient = CreateMockApiClient();
        List<TenantSummary> tenants = [new("t1", "Tenant One", TenantStatusType.Active, 100, 2)];
        List<AggregateTypeInfo> types = [new("Counter", "Counting", 3, 2, true)];

        apiClient.GetTenantsAsync(Arg.Any<CancellationToken>())
            .Returns(
                _ => Task.FromResult<IReadOnlyList<TenantSummary>>(tenants),
                _ => throw new OperationCanceledException());
        apiClient.GetAggregateTypesAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(types);

        TopologyCacheService sut = new(apiClient);

        await sut.EnsureLoadedAsync();
        await sut.RefreshAsync();

        sut.IsLoaded.ShouldBeTrue();
        sut.Tenants.Count.ShouldBe(1);
        sut.Tenants[0].TenantId.ShouldBe("t1");
    }

    [Fact]
    public async Task EnsureLoadedAsync_Succeeds_WhenTenantsFailButDomainsLoad()
    {
        AdminStreamApiClient apiClient = CreateMockApiClient();
        List<AggregateTypeInfo> types = [new("Counter", "Counting", 3, 2, true)];

        apiClient.GetTenantsAsync(Arg.Any<CancellationToken>())
            .Returns<Task<IReadOnlyList<TenantSummary>>>(_ => throw new OperationCanceledException());
        apiClient.GetAggregateTypesAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(types);

        TopologyCacheService sut = new(apiClient);

        await sut.EnsureLoadedAsync();

        sut.IsLoaded.ShouldBeTrue();
        sut.Tenants.ShouldBeEmpty();
        sut.Domains.Count.ShouldBe(1);
        sut.Domains[0].ShouldBe("Counting");
    }
}
