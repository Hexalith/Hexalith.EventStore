namespace Hexalith.EventStore.Admin.Mcp.Tests;

using System.Net;

using Hexalith.EventStore.Admin.Mcp.Tests.TestHelpers;

public class AdminApiClientTenantTests
{
    private static readonly string _tenantListJson = """[{"tenantId":"t1","displayName":"Acme Corp","status":0,"eventCount":500,"domainCount":3}]""";
    private static readonly string _tenantDetailJson = """{"tenantId":"t1","displayName":"Acme Corp","status":0,"eventCount":500,"domainCount":3,"storageBytes":1048576,"createdAtUtc":"2026-01-01T00:00:00Z","quotas":null,"subscriptionTier":"Standard"}""";
    private static readonly string _tenantQuotasJson = """{"tenantId":"t1","maxEventsPerDay":10000,"maxStorageBytes":10737418240,"currentUsage":1048576}""";
    private static readonly string _tenantUsersJson = """[{"email":"admin@acme.com","role":"Admin","addedAtUtc":"2026-01-01T00:00:00Z"}]""";

    [Fact]
    public async Task ListTenantsAsync_SendsGetToCorrectPath()
    {
        Uri? capturedUri = null;
        using HttpClient httpClient = MockHttpMessageHandler.CreateCapturingClient(
            r => capturedUri = r.RequestUri,
            HttpStatusCode.OK,
            _tenantListJson);
        var client = new AdminApiClient(httpClient);

        _ = await client.ListTenantsAsync(CancellationToken.None);

        capturedUri.ShouldNotBeNull();
        capturedUri.PathAndQuery.ShouldBe("/api/v1/admin/tenants");
    }

    [Fact]
    public async Task GetTenantDetailAsync_SendsGetToCorrectPath()
    {
        Uri? capturedUri = null;
        using HttpClient httpClient = MockHttpMessageHandler.CreateCapturingClient(
            r => capturedUri = r.RequestUri,
            HttpStatusCode.OK,
            _tenantDetailJson);
        var client = new AdminApiClient(httpClient);

        _ = await client.GetTenantDetailAsync("t1", CancellationToken.None);

        capturedUri.ShouldNotBeNull();
        capturedUri.PathAndQuery.ShouldBe("/api/v1/admin/tenants/t1");
    }

    [Fact]
    public async Task GetTenantQuotasAsync_SendsGetToCorrectPath()
    {
        Uri? capturedUri = null;
        using HttpClient httpClient = MockHttpMessageHandler.CreateCapturingClient(
            r => capturedUri = r.RequestUri,
            HttpStatusCode.OK,
            _tenantQuotasJson);
        var client = new AdminApiClient(httpClient);

        _ = await client.GetTenantQuotasAsync("t1", CancellationToken.None);

        capturedUri.ShouldNotBeNull();
        capturedUri.PathAndQuery.ShouldBe("/api/v1/admin/tenants/t1/quotas");
    }

    [Fact]
    public async Task GetTenantUsersAsync_SendsGetToCorrectPath()
    {
        Uri? capturedUri = null;
        using HttpClient httpClient = MockHttpMessageHandler.CreateCapturingClient(
            r => capturedUri = r.RequestUri,
            HttpStatusCode.OK,
            _tenantUsersJson);
        var client = new AdminApiClient(httpClient);

        _ = await client.GetTenantUsersAsync("t1", CancellationToken.None);

        capturedUri.ShouldNotBeNull();
        capturedUri.PathAndQuery.ShouldBe("/api/v1/admin/tenants/t1/users");
    }

    [Theory]
    [InlineData("simple-tenant", "simple-tenant")]
    [InlineData("tenant/with/slashes", "tenant%2Fwith%2Fslashes")]
    [InlineData("tenant with spaces", "tenant%20with%20spaces")]
    [InlineData("tenant+plus", "tenant%2Bplus")]
    public async Task GetTenantDetailAsync_UriEncodesTenantId(string tenantId, string expectedEncoded)
    {
        Uri? capturedUri = null;
        using HttpClient httpClient = MockHttpMessageHandler.CreateCapturingClient(
            r => capturedUri = r.RequestUri,
            HttpStatusCode.OK,
            _tenantDetailJson);
        var client = new AdminApiClient(httpClient);

        _ = await client.GetTenantDetailAsync(tenantId, CancellationToken.None);

        capturedUri.ShouldNotBeNull();
        capturedUri.PathAndQuery.ShouldBe($"/api/v1/admin/tenants/{expectedEncoded}");
    }

    [Theory]
    [InlineData("simple-tenant", "simple-tenant")]
    [InlineData("tenant/with/slashes", "tenant%2Fwith%2Fslashes")]
    [InlineData("tenant with spaces", "tenant%20with%20spaces")]
    [InlineData("tenant+plus", "tenant%2Bplus")]
    public async Task GetTenantQuotasAsync_UriEncodesTenantId(string tenantId, string expectedEncoded)
    {
        Uri? capturedUri = null;
        using HttpClient httpClient = MockHttpMessageHandler.CreateCapturingClient(
            r => capturedUri = r.RequestUri,
            HttpStatusCode.OK,
            _tenantQuotasJson);
        var client = new AdminApiClient(httpClient);

        _ = await client.GetTenantQuotasAsync(tenantId, CancellationToken.None);

        capturedUri.ShouldNotBeNull();
        capturedUri.PathAndQuery.ShouldBe($"/api/v1/admin/tenants/{expectedEncoded}/quotas");
    }

    [Theory]
    [InlineData("simple-tenant", "simple-tenant")]
    [InlineData("tenant/with/slashes", "tenant%2Fwith%2Fslashes")]
    [InlineData("tenant with spaces", "tenant%20with%20spaces")]
    [InlineData("tenant+plus", "tenant%2Bplus")]
    public async Task GetTenantUsersAsync_UriEncodesTenantId(string tenantId, string expectedEncoded)
    {
        Uri? capturedUri = null;
        using HttpClient httpClient = MockHttpMessageHandler.CreateCapturingClient(
            r => capturedUri = r.RequestUri,
            HttpStatusCode.OK,
            _tenantUsersJson);
        var client = new AdminApiClient(httpClient);

        _ = await client.GetTenantUsersAsync(tenantId, CancellationToken.None);

        capturedUri.ShouldNotBeNull();
        capturedUri.PathAndQuery.ShouldBe($"/api/v1/admin/tenants/{expectedEncoded}/users");
    }

    [Fact]
    public async Task ListTenantsAsync_ReturnsEmptyList_WhenNullResponse()
    {
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, "null");
        var client = new AdminApiClient(httpClient);

        var result = await client.ListTenantsAsync(CancellationToken.None);

        result.ShouldNotBeNull();
        result.Count.ShouldBe(0);
    }

    [Fact]
    public async Task GetTenantUsersAsync_ReturnsEmptyList_WhenNullResponse()
    {
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, "null");
        var client = new AdminApiClient(httpClient);

        var result = await client.GetTenantUsersAsync("t1", CancellationToken.None);

        result.ShouldNotBeNull();
        result.Count.ShouldBe(0);
    }
}
