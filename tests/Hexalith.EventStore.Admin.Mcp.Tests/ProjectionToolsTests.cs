
using System.Net;
using System.Text.Json;

using Hexalith.EventStore.Admin.Mcp.Tools;
using Hexalith.EventStore.Testing.Http;

namespace Hexalith.EventStore.Admin.Mcp.Tests;

public class ProjectionToolsTests {
    private static readonly string _projectionListJson = """[{"name":"OrderSummary","tenantId":"t1","status":0,"lag":10,"throughput":5.0,"errorCount":0,"lastProcessedPosition":100,"lastProcessedUtc":"2026-01-01T00:00:00Z"}]""";
    private static readonly string _projectionDetailJson = """{"name":"OrderSummary","tenantId":"t1","status":0,"lag":10,"throughput":5.0,"errorCount":0,"lastProcessedPosition":100,"lastProcessedUtc":"2026-01-01T00:00:00Z","errors":[],"configuration":"{}","subscribedEventTypes":["OrderPlaced"]}""";

    [Fact]
    public async Task ListProjections_ReturnsValidJson_OnSuccess() {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, _projectionListJson);
        var client = new AdminApiClient(httpClient);

        string result = await ProjectionTools.ListProjections(client, new InvestigationSession(), cancellationToken: ct);

        using var doc = JsonDocument.Parse(result);
        doc.RootElement.ValueKind.ShouldBe(JsonValueKind.Array);
    }

    [Fact]
    public async Task ListProjections_ReturnsErrorJson_OnFailure() {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using HttpClient httpClient = MockHttpMessageHandler.CreateThrowingClient(new HttpRequestException("Connection refused"));
        var client = new AdminApiClient(httpClient);

        string result = await ProjectionTools.ListProjections(client, new InvestigationSession(), cancellationToken: ct);

        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("adminApiStatus").GetString().ShouldBe("unreachable");
    }

    [Fact]
    public async Task GetProjectionDetail_ReturnsValidJson_OnSuccess() {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, _projectionDetailJson);
        var client = new AdminApiClient(httpClient);

        string result = await ProjectionTools.GetProjectionDetail(client, new InvestigationSession(), "t1", "OrderSummary", ct);

        using var doc = JsonDocument.Parse(result);
        doc.RootElement.TryGetProperty("error", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task GetProjectionDetail_ReturnsNotFound_OnNull() {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, "null");
        var client = new AdminApiClient(httpClient);

        string result = await ProjectionTools.GetProjectionDetail(client, new InvestigationSession(), "t1", "NonExistent", ct);

        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("adminApiStatus").GetString().ShouldBe("not-found");
    }
}
