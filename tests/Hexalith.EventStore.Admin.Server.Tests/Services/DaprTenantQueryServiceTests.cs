using Dapr.Client;

using Hexalith.EventStore.Admin.Abstractions.Models.Tenants;
using Hexalith.EventStore.Admin.Server.Configuration;
using Hexalith.EventStore.Admin.Server.Services;
using Hexalith.EventStore.Admin.Server.Tests.Helpers;

using Microsoft.Extensions.Options;

using NSubstitute;

namespace Hexalith.EventStore.Admin.Server.Tests.Services;

public class DaprTenantQueryServiceTests {
    private const string TenantServiceAppId = "tenants";

    private static (DaprTenantQueryService Service, TestHttpMessageHandler Handler) CreateService(
        DaprClient? daprClient = null,
        IAdminAuthContext? authContext = null) {
        daprClient ??= Substitute.For<DaprClient>();
        authContext ??= new NullAdminAuthContext();
        IOptions<AdminServerOptions> options = Options.Create(new AdminServerOptions {
            TenantServiceAppId = TenantServiceAppId,
        });

        var handler = new TestHttpMessageHandler();
        HttpClient httpClient = new(handler) { BaseAddress = new Uri("http://localhost") };
        IHttpClientFactory httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient(Arg.Any<string>()).Returns(httpClient);

        var service = new DaprTenantQueryService(
            daprClient,
            httpClientFactory,
            options,
            authContext);

        return (service, handler);
    }

    [Fact]
    public async Task ListTenantsAsync_ReturnsTenants_WhenServiceAvailable() {
        var tenants = new List<TenantSummary>
        {
            new("tenant1", "Acme Corp", TenantStatusType.Active, 1000, 5),
            new("tenant2", "Widget Co", TenantStatusType.Suspended, 500, 2),
        };

        (DaprTenantQueryService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupJsonResponse<IReadOnlyList<TenantSummary>>(tenants);

        IReadOnlyList<TenantSummary> result = await service.ListTenantsAsync();

        result.Count.ShouldBe(2);
        result[0].TenantId.ShouldBe("tenant1");
    }

    [Fact]
    public async Task ListTenantsAsync_Throws_WhenServiceUnavailable() {
        (DaprTenantQueryService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupException(new InvalidOperationException("Tenants service down"));

        await Should.ThrowAsync<InvalidOperationException>(
            () => service.ListTenantsAsync());
    }

    [Fact]
    public async Task GetTenantQuotasAsync_ReturnsQuotas_WhenServiceAvailable() {
        var quotas = new TenantQuotas("tenant1", 10000, 1000000, 500000);

        (DaprTenantQueryService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupJsonResponse(quotas);

        TenantQuotas result = await service.GetTenantQuotasAsync("tenant1");

        result.TenantId.ShouldBe("tenant1");
        result.MaxEventsPerDay.ShouldBe(10000);
    }

    [Fact]
    public async Task GetTenantQuotasAsync_Throws_WhenServiceUnavailable() {
        (DaprTenantQueryService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupException(new InvalidOperationException("Tenants service down"));

        await Should.ThrowAsync<InvalidOperationException>(
            () => service.GetTenantQuotasAsync("tenant1"));
    }

    [Fact]
    public async Task CompareTenantUsageAsync_Throws_WhenServiceUnavailable() {
        (DaprTenantQueryService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupException(new InvalidOperationException("Tenants service down"));

        await Should.ThrowAsync<InvalidOperationException>(
            () => service.CompareTenantUsageAsync(["tenant1", "tenant2"]));
    }

    [Fact]
    public async Task ListTenantsAsync_PropagatesCancellation()
    {
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        (DaprTenantQueryService service, TestHttpMessageHandler handler) = CreateService();
        handler.SetupException(new OperationCanceledException());

        await Should.ThrowAsync<OperationCanceledException>(
            () => service.ListTenantsAsync(cts.Token));
    }

    [Fact]
    public async Task CompareTenantUsageAsync_ReturnsEmpty_WhenTenantIdsEmpty()
    {
        (DaprTenantQueryService service, _) = CreateService();

        TenantComparison result = await service.CompareTenantUsageAsync([]);

        result.Tenants.ShouldBeEmpty();
    }

    [Fact]
    public async Task CompareTenantUsageAsync_DelegatesToTenantService_WithBearerToken() {
        IAdminAuthContext authContext = Substitute.For<IAdminAuthContext>();
        authContext.GetToken().Returns("tenant-token");
        TenantComparison expected = new(
            [new TenantSummary("tenant1", "Acme Corp", TenantStatusType.Active, 1000, 5)],
            DateTimeOffset.UtcNow);

        (DaprTenantQueryService service, TestHttpMessageHandler handler) = CreateService(authContext: authContext);
        handler.SetupJsonResponse(expected);

        TenantComparison result = await service.CompareTenantUsageAsync(["tenant1", "tenant2"]);

        result.Tenants.Count.ShouldBe(1);
        result.Tenants[0].TenantId.ShouldBe("tenant1");
        handler.LastRequest.ShouldNotBeNull();
        handler.LastRequest!.Method.ShouldBe(HttpMethod.Post);
        handler.LastRequest.RequestUri!.ToString().ShouldContain("api/v1/tenants/compare");
        handler.LastRequest.Headers.Authorization!.Parameter.ShouldBe("tenant-token");
    }
}
