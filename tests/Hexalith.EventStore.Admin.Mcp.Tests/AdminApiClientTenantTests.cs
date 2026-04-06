namespace Hexalith.EventStore.Admin.Mcp.Tests;

using System.Net;

using Hexalith.EventStore.Testing.Http;

public class AdminApiClientTenantTests
{
    private static readonly string _tenantListJson = """[{"tenantId":"t1","name":"Acme Corp","status":0}]""";
    private static readonly string _tenantDetailJson = """{"tenantId":"t1","name":"Acme Corp","description":null,"status":0,"createdAt":"2026-01-01T00:00:00Z"}""";
    private static readonly string _tenantUsersJson = """[{"userId":"admin-001","role":"Admin"}]""";

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
