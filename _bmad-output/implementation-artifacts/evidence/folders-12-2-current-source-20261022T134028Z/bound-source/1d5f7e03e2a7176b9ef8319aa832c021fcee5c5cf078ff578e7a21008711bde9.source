
using System.Net;
using System.Text.Json;

using Hexalith.EventStore.Admin.Mcp.Tools;
using Hexalith.EventStore.Testing.Http;

namespace Hexalith.EventStore.Admin.Mcp.Tests;

public class StorageToolsTests {
    private static readonly string _storageJson = """{"totalEventCount":1000,"totalSizeBytes":50000,"tenantBreakdown":[{"tenantId":"t1","eventCount":500,"sizeBytes":25000,"growthRatePerDay":10.5}],"totalStreamCount":50}""";

    [Fact]
    public async Task GetStorageOverview_ReturnsValidJson_OnSuccess() {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, _storageJson);
        var client = new AdminApiClient(httpClient);

        string result = await StorageTools.GetStorageOverview(client, new InvestigationSession(), cancellationToken: ct);

        using var doc = JsonDocument.Parse(result);
        doc.RootElement.TryGetProperty("error", out _).ShouldBeFalse();
        doc.RootElement.GetProperty("totalEventCount").GetInt64().ShouldBe(1000);
    }

    [Fact]
    public async Task GetStorageOverview_ReturnsErrorJson_OnFailure() {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using HttpClient httpClient = MockHttpMessageHandler.CreateThrowingClient(new HttpRequestException("Connection refused"));
        var client = new AdminApiClient(httpClient);

        string result = await StorageTools.GetStorageOverview(client, new InvestigationSession(), cancellationToken: ct);

        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("adminApiStatus").GetString().ShouldBe("unreachable");
    }

    [Fact]
    public async Task GetStorageOverview_PassesTenantIdFilter() {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Uri? capturedUri = null;
        using HttpClient httpClient = MockHttpMessageHandler.CreateCapturingClient(
            r => capturedUri = r.RequestUri,
            HttpStatusCode.OK,
            _storageJson);
        var client = new AdminApiClient(httpClient);

        _ = await StorageTools.GetStorageOverview(client, new InvestigationSession(), tenantId: "tenant1", cancellationToken: ct);

        _ = capturedUri.ShouldNotBeNull();
        capturedUri.PathAndQuery.ShouldContain("tenantId=tenant1");
    }
}
