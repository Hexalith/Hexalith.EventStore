namespace Hexalith.EventStore.Admin.Mcp.Tests;

using System.Net;
using System.Text.Json;

using Hexalith.EventStore.Testing.Http;
using Hexalith.EventStore.Admin.Mcp.Tools;

public class TenantToolsTests
{
    private static readonly string _tenantListJson = """[{"tenantId":"t1","displayName":"Acme Corp","status":0,"eventCount":500,"domainCount":3}]""";
    private static readonly string _tenantDetailJson = """{"tenantId":"t1","displayName":"Acme Corp","status":0,"eventCount":500,"domainCount":3,"storageBytes":1048576,"createdAtUtc":"2026-01-01T00:00:00Z","quotas":null,"subscriptionTier":"Standard"}""";
    private static readonly string _tenantQuotasJson = """{"tenantId":"t1","maxEventsPerDay":10000,"maxStorageBytes":10737418240,"currentUsage":1048576}""";
    private static readonly string _tenantUsersJson = """[{"email":"admin@acme.com","role":"Admin","addedAtUtc":"2026-01-01T00:00:00Z"}]""";

    [Fact]
    public async Task TenantList_ReturnsValidJson_OnSuccess()
    {
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, _tenantListJson);
        var client = new AdminApiClient(httpClient);

        string result = await TenantTools.ListTenants(client);

        using JsonDocument doc = JsonDocument.Parse(result);
        doc.RootElement.ValueKind.ShouldBe(JsonValueKind.Array);
        doc.RootElement.GetArrayLength().ShouldBe(1);
    }

    [Fact]
    public async Task TenantList_ReturnsEmptyArrayJson_WhenNoTenants()
    {
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, "null");
        var client = new AdminApiClient(httpClient);

        string result = await TenantTools.ListTenants(client);

        using JsonDocument doc = JsonDocument.Parse(result);
        doc.RootElement.ValueKind.ShouldBe(JsonValueKind.Array);
        doc.RootElement.GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task TenantList_ReturnsErrorJson_OnFailure()
    {
        using HttpClient httpClient = MockHttpMessageHandler.CreateThrowingClient(new HttpRequestException("Connection refused"));
        var client = new AdminApiClient(httpClient);

        string result = await TenantTools.ListTenants(client);

        using JsonDocument doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("error").GetBoolean().ShouldBeTrue();
        doc.RootElement.GetProperty("adminApiStatus").GetString().ShouldBe("unreachable");
    }

    [Fact]
    public async Task TenantDetail_ReturnsValidJson_OnSuccess()
    {
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, _tenantDetailJson);
        var client = new AdminApiClient(httpClient);

        string result = await TenantTools.GetTenantDetail(client, "t1");

        using JsonDocument doc = JsonDocument.Parse(result);
        doc.RootElement.TryGetProperty("error", out _).ShouldBeFalse();
        doc.RootElement.GetProperty("tenantId").GetString().ShouldBe("t1");
    }

    [Fact]
    public async Task TenantDetail_ReturnsNotFoundError_On404()
    {
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, "null");
        var client = new AdminApiClient(httpClient);

        string result = await TenantTools.GetTenantDetail(client, "nonexistent");

        using JsonDocument doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("adminApiStatus").GetString().ShouldBe("not-found");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task TenantDetail_ReturnsValidationError_WhenTenantIdEmpty(string tenantId)
    {
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, _tenantDetailJson);
        var client = new AdminApiClient(httpClient);

        string result = await TenantTools.GetTenantDetail(client, tenantId);

        using JsonDocument doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("error").GetBoolean().ShouldBeTrue();
        doc.RootElement.GetProperty("adminApiStatus").GetString().ShouldBe("invalid-input");
    }

    [Fact]
    public async Task TenantQuotas_ReturnsValidJson_OnSuccess()
    {
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, _tenantQuotasJson);
        var client = new AdminApiClient(httpClient);

        string result = await TenantTools.GetTenantQuotas(client, "t1");

        using JsonDocument doc = JsonDocument.Parse(result);
        doc.RootElement.TryGetProperty("error", out _).ShouldBeFalse();
        doc.RootElement.GetProperty("tenantId").GetString().ShouldBe("t1");
    }

    [Fact]
    public async Task TenantQuotas_ReturnsNotFoundError_On404()
    {
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, "null");
        var client = new AdminApiClient(httpClient);

        string result = await TenantTools.GetTenantQuotas(client, "nonexistent");

        using JsonDocument doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("adminApiStatus").GetString().ShouldBe("not-found");
    }

    [Fact]
    public async Task TenantUsers_ReturnsValidJson_OnSuccess()
    {
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, _tenantUsersJson);
        var client = new AdminApiClient(httpClient);

        string result = await TenantTools.GetTenantUsers(client, "t1");

        using JsonDocument doc = JsonDocument.Parse(result);
        doc.RootElement.ValueKind.ShouldBe(JsonValueKind.Array);
        doc.RootElement.GetArrayLength().ShouldBe(1);
    }

    [Fact]
    public async Task TenantUsers_ReturnsEmptyArrayJson_WhenNoUsers()
    {
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, "null");
        var client = new AdminApiClient(httpClient);

        string result = await TenantTools.GetTenantUsers(client, "t1");

        using JsonDocument doc = JsonDocument.Parse(result);
        doc.RootElement.ValueKind.ShouldBe(JsonValueKind.Array);
        doc.RootElement.GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task TenantTools_ReturnServiceUnavailableError_On503()
    {
        using HttpClient httpClient = MockHttpMessageHandler.CreateThrowingClient(
            new HttpRequestException("Service Unavailable", null, HttpStatusCode.ServiceUnavailable));
        var client = new AdminApiClient(httpClient);

        string result = await TenantTools.ListTenants(client);

        using JsonDocument doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("error").GetBoolean().ShouldBeTrue();
        doc.RootElement.GetProperty("adminApiStatus").GetString().ShouldBe("service-unavailable");
    }

    [Fact]
    public async Task AllTenantTools_ReturnParseableJson()
    {
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, _tenantListJson);
        var client = new AdminApiClient(httpClient);

        string result = await TenantTools.ListTenants(client);

        Should.NotThrow(() => JsonDocument.Parse(result));
    }
}
