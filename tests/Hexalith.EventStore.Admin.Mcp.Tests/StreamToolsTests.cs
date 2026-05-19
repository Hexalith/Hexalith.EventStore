
using System.Net;
using System.Text.Json;

using Hexalith.EventStore.Admin.Mcp.Tools;
using Hexalith.EventStore.Testing.Http;

namespace Hexalith.EventStore.Admin.Mcp.Tests;

public class StreamToolsTests {
    private static readonly string _streamsJson = """{"items":[{"tenantId":"t1","domain":"Orders","aggregateId":"o1","lastEventSequence":5,"lastActivityUtc":"2026-01-01T00:00:00Z","eventCount":5,"hasSnapshot":false,"streamStatus":0}],"totalCount":1,"continuationToken":null}""";
    private static readonly string _timelineJson = """{"items":[{"sequenceNumber":1,"timestamp":"2026-01-01T00:00:00Z","entryType":1,"typeName":"OrderPlaced","correlationId":"c1","userId":null}],"totalCount":1,"continuationToken":null}""";
    private static readonly string _stateJson = """{"tenantId":"t1","domain":"Orders","aggregateId":"o1","sequenceNumber":3,"timestamp":"2026-01-01T00:00:00Z","stateJson":"{}"}""";
    private static readonly string _eventDetailJson = """{"tenantId":"t1","domain":"Orders","aggregateId":"o1","sequenceNumber":1,"eventTypeName":"OrderPlaced","timestamp":"2026-01-01T00:00:00Z","correlationId":"c1","causationId":null,"userId":null,"payloadJson":"{}"}""";

    [Fact]
    public async Task ListStreams_ReturnsValidJson_OnSuccess() {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, _streamsJson);
        var client = new AdminApiClient(httpClient);

        string result = await StreamTools.ListStreams(client, new InvestigationSession(), cancellationToken: ct);

        using var doc = JsonDocument.Parse(result);
        doc.RootElement.TryGetProperty("error", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task ListStreams_ReturnsUnreachableError_OnConnectionFailure() {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using HttpClient httpClient = MockHttpMessageHandler.CreateThrowingClient(new HttpRequestException("Connection refused"));
        var client = new AdminApiClient(httpClient);

        string result = await StreamTools.ListStreams(client, new InvestigationSession(), cancellationToken: ct);

        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("adminApiStatus").GetString().ShouldBe("unreachable");
    }

    [Fact]
    public async Task ListStreams_ReturnsUnauthorizedError_OnHttp401() {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using HttpClient httpClient = MockHttpMessageHandler.CreateThrowingClient(
            new HttpRequestException("Unauthorized", null, HttpStatusCode.Unauthorized));
        var client = new AdminApiClient(httpClient);

        string result = await StreamTools.ListStreams(client, new InvestigationSession(), cancellationToken: ct);

        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("adminApiStatus").GetString().ShouldBe("unauthorized");
    }

    [Fact]
    public async Task GetStreamEvents_ReturnsValidJson_OnSuccess() {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, _timelineJson);
        var client = new AdminApiClient(httpClient);

        string result = await StreamTools.GetStreamEvents(client, new InvestigationSession(), "t1", "Orders", "o1", cancellationToken: ct);

        using var doc = JsonDocument.Parse(result);
        doc.RootElement.TryGetProperty("error", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task GetStreamState_ReturnsValidJson_OnSuccess() {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, _stateJson);
        var client = new AdminApiClient(httpClient);

        string result = await StreamTools.GetStreamState(client, new InvestigationSession(), "t1", "Orders", "o1", 3, ct);

        using var doc = JsonDocument.Parse(result);
        doc.RootElement.TryGetProperty("error", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task GetStreamState_ReturnsNotFound_WhenNull() {
        CancellationToken ct = TestContext.Current.CancellationToken;
        // Return 200 with JSON null body — GetFromJsonAsync returns null
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, "null");
        var client = new AdminApiClient(httpClient);

        string result = await StreamTools.GetStreamState(client, new InvestigationSession(), "t1", "Orders", "o1", 999, ct);

        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("adminApiStatus").GetString().ShouldBe("not-found");
    }

    [Fact]
    public async Task GetEventDetail_ReturnsValidJson_OnSuccess() {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, _eventDetailJson);
        var client = new AdminApiClient(httpClient);

        string result = await StreamTools.GetEventDetail(client, new InvestigationSession(), "t1", "Orders", "o1", 1, ct);

        using var doc = JsonDocument.Parse(result);
        doc.RootElement.TryGetProperty("error", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task GetEventDetail_ReturnsNotFound_WhenNull() {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, "null");
        var client = new AdminApiClient(httpClient);

        string result = await StreamTools.GetEventDetail(client, new InvestigationSession(), "t1", "Orders", "o1", 999, ct);

        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("adminApiStatus").GetString().ShouldBe("not-found");
    }

    [Fact]
    public async Task AllStreamTools_ReturnParseableJson() {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, _streamsJson);
        var client = new AdminApiClient(httpClient);

        string result = await StreamTools.ListStreams(client, new InvestigationSession(), cancellationToken: ct);

        _ = Should.NotThrow(() => JsonDocument.Parse(result));
    }

    [Fact]
    public async Task GetEventDetail_SentinelBearingPayloadJson_DoesNotLeak() {
        // P1 — sentinel injection at the per-tool boundary (not just ToolHelper) proves the no-leak
        // invariant flows through StreamTools.GetEventDetail's SerializeResult call.
        CancellationToken ct = TestContext.Current.CancellationToken;
        string sentinelBearing = $$"""{"tenantId":"t1","domain":"Orders","aggregateId":"o1","sequenceNumber":1,"eventTypeName":"OrderPlaced","timestamp":"2026-01-01T00:00:00Z","correlationId":"c1","causationId":null,"userId":null,"payloadJson":"{{Testing.Security.ProtectedDataLeakSentinel.ProtectedPayloadPlaintext}}"}""";
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, sentinelBearing);
        var client = new AdminApiClient(httpClient);

        string result = await StreamTools.GetEventDetail(client, new InvestigationSession(), "t1", "Orders", "o1", 1, ct);

        Testing.Security.ProtectedDataLeakSentinel.AssertNoLeak([result]);
        result.ShouldNotContain("payloadJson");
        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("payload").GetProperty("placeholder").GetString().ShouldBe("Protected content redacted.");
    }
}
