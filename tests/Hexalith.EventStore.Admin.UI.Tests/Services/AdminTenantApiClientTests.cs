using System.Net;

using Hexalith.EventStore.Admin.Abstractions.Models.Tenants;
using Hexalith.EventStore.Admin.UI.Services;
using Hexalith.EventStore.Admin.UI.Services.Exceptions;
using Hexalith.EventStore.Testing.Http;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

namespace Hexalith.EventStore.Admin.UI.Tests.Services;

public class AdminTenantApiClientTests
{
    private static AdminTenantApiClient CreateClient(HttpClient httpClient)
    {
        IHttpClientFactory factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("AdminApi").Returns(httpClient);
        return new AdminTenantApiClient(factory, NullLogger<AdminTenantApiClient>.Instance);
    }

    // === ListTenantsAsync ===

    [Fact]
    public async Task ListTenantsAsync_ReturnsTenants_WhenApiResponds()
    {
        string json = """[{"tenantId":"t1","displayName":"Tenant 1","status":0,"eventCount":100,"domainCount":3}]""";
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, json);

        AdminTenantApiClient client = CreateClient(httpClient);

        IReadOnlyList<TenantSummary> result = await client.ListTenantsAsync();

        result.Count.ShouldBe(1);
        result[0].TenantId.ShouldBe("t1");
        result[0].DisplayName.ShouldBe("Tenant 1");
    }

    [Fact]
    public async Task ListTenantsAsync_ThrowsServiceUnavailable_WhenApiReturnsError()
    {
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.InternalServerError, "{}");

        AdminTenantApiClient client = CreateClient(httpClient);

        await Should.ThrowAsync<ServiceUnavailableException>(
            () => client.ListTenantsAsync());
    }

    [Fact]
    public async Task ListTenantsAsync_ThrowsUnauthorized_When401()
    {
        using HttpClient httpClient = MockHttpMessageHandler.CreateStatusClient(HttpStatusCode.Unauthorized);

        AdminTenantApiClient client = CreateClient(httpClient);

        await Should.ThrowAsync<UnauthorizedAccessException>(
            () => client.ListTenantsAsync());
    }

    // === GetTenantDetailAsync ===

    [Fact]
    public async Task GetTenantDetailAsync_ReturnsDetail_WhenApiResponds()
    {
        string json = """{"tenantId":"t1","displayName":"Tenant 1","status":0,"eventCount":100,"domainCount":5,"storageBytes":50000,"createdAtUtc":"2026-01-01T00:00:00Z","quotas":null,"subscriptionTier":null}""";
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, json);

        AdminTenantApiClient client = CreateClient(httpClient);

        TenantDetail? result = await client.GetTenantDetailAsync("t1");

        result.ShouldNotBeNull();
        result.TenantId.ShouldBe("t1");
        result.DisplayName.ShouldBe("Tenant 1");
        result.EventCount.ShouldBe(100);
        result.DomainCount.ShouldBe(5);
    }

    [Fact]
    public async Task GetTenantDetailAsync_ThrowsServiceUnavailable_WhenApiReturnsError()
    {
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.InternalServerError, "{}");

        AdminTenantApiClient client = CreateClient(httpClient);

        await Should.ThrowAsync<ServiceUnavailableException>(
            () => client.GetTenantDetailAsync("t1"));
    }

    [Fact]
    public async Task GetTenantDetailAsync_ReturnsNull_WhenNotFound()
    {
        using HttpClient httpClient = MockHttpMessageHandler.CreateStatusClient(HttpStatusCode.NotFound);

        AdminTenantApiClient client = CreateClient(httpClient);

        TenantDetail? result = await client.GetTenantDetailAsync("missing");

        result.ShouldBeNull();
    }
}
