namespace Hexalith.EventStore.Admin.Mcp.Tests;

using System.Net;
using System.Text.Json;

using Hexalith.EventStore.Testing.Http;
using Hexalith.EventStore.Admin.Mcp.Tools;

public class TenantToolsTests
{
    private static readonly string _tenantListJson = """[{"tenantId":"t1","name":"Acme Corp","status":0}]""";
    private static readonly string _tenantDetailJson = """{"tenantId":"t1","name":"Acme Corp","description":null,"status":0,"createdAt":"2026-01-01T00:00:00Z"}""";
    private static readonly string _tenantUsersJson = """[{"userId":"admin-001","role":"Admin"}]""";

    [Fact]
    public async Task TenantList_ReturnsValidJson_OnSuccess()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, _tenantListJson);
        var client = new AdminApiClient(httpClient);

        string result = await TenantTools.ListTenants(client, ct);

        using JsonDocument doc = JsonDocument.Parse(result);
        doc.RootElement.ValueKind.ShouldBe(JsonValueKind.Array);
        doc.RootElement.GetArrayLength().ShouldBe(1);
    }

    [Fact]
    public async Task TenantList_ReturnsEmptyArrayJson_WhenNoTenants()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, "null");
        var client = new AdminApiClient(httpClient);

        string result = await TenantTools.ListTenants(client, ct);

        using JsonDocument doc = JsonDocument.Parse(result);
        doc.RootElement.ValueKind.ShouldBe(JsonValueKind.Array);
        doc.RootElement.GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task TenantList_ReturnsErrorJson_OnFailure()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using HttpClient httpClient = MockHttpMessageHandler.CreateThrowingClient(new HttpRequestException("Connection refused"));
        var client = new AdminApiClient(httpClient);

        string result = await TenantTools.ListTenants(client, ct);

        using JsonDocument doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("error").GetBoolean().ShouldBeTrue();
        doc.RootElement.GetProperty("adminApiStatus").GetString().ShouldBe("unreachable");
    }

    [Fact]
    public async Task TenantDetail_ReturnsValidJson_OnSuccess()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, _tenantDetailJson);
        var client = new AdminApiClient(httpClient);

        string result = await TenantTools.GetTenantDetail(client, "t1", ct);

        using JsonDocument doc = JsonDocument.Parse(result);
        doc.RootElement.TryGetProperty("error", out _).ShouldBeFalse();
        doc.RootElement.GetProperty("tenantId").GetString().ShouldBe("t1");
    }

    [Fact]
    public async Task TenantDetail_ReturnsNotFoundError_On404()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, "null");
        var client = new AdminApiClient(httpClient);

        string result = await TenantTools.GetTenantDetail(client, "nonexistent", ct);

        using JsonDocument doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("adminApiStatus").GetString().ShouldBe("not-found");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task TenantDetail_ReturnsValidationError_WhenTenantIdEmpty(string tenantId)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, _tenantDetailJson);
        var client = new AdminApiClient(httpClient);

        string result = await TenantTools.GetTenantDetail(client, tenantId, ct);

        using JsonDocument doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("error").GetBoolean().ShouldBeTrue();
        doc.RootElement.GetProperty("adminApiStatus").GetString().ShouldBe("invalid-input");
    }

    [Fact]
    public async Task TenantUsers_ReturnsValidJson_OnSuccess()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, _tenantUsersJson);
        var client = new AdminApiClient(httpClient);

        string result = await TenantTools.GetTenantUsers(client, "t1", ct);

        using JsonDocument doc = JsonDocument.Parse(result);
        doc.RootElement.ValueKind.ShouldBe(JsonValueKind.Array);
        doc.RootElement.GetArrayLength().ShouldBe(1);
    }

    [Fact]
    public async Task TenantUsers_ReturnsEmptyArrayJson_WhenNoUsers()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, "null");
        var client = new AdminApiClient(httpClient);

        string result = await TenantTools.GetTenantUsers(client, "t1", ct);

        using JsonDocument doc = JsonDocument.Parse(result);
        doc.RootElement.ValueKind.ShouldBe(JsonValueKind.Array);
        doc.RootElement.GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task TenantTools_ReturnServiceUnavailableError_On503()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using HttpClient httpClient = MockHttpMessageHandler.CreateThrowingClient(
            new HttpRequestException("Service Unavailable", null, HttpStatusCode.ServiceUnavailable));
        var client = new AdminApiClient(httpClient);

        string result = await TenantTools.ListTenants(client, ct);

        using JsonDocument doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("error").GetBoolean().ShouldBeTrue();
        doc.RootElement.GetProperty("adminApiStatus").GetString().ShouldBe("service-unavailable");
    }

    [Fact]
    public async Task AllTenantTools_ReturnParseableJson()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, _tenantListJson);
        var client = new AdminApiClient(httpClient);

        string result = await TenantTools.ListTenants(client, ct);

        Should.NotThrow(() => JsonDocument.Parse(result));
    }
}
