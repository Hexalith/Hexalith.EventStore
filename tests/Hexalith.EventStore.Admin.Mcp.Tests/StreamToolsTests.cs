namespace Hexalith.EventStore.Admin.Mcp.Tests;

using System.Net;
using System.Text.Json;

using Hexalith.EventStore.Admin.Mcp.Tests.TestHelpers;
using Hexalith.EventStore.Admin.Mcp.Tools;

public class StreamToolsTests
{
    private static readonly string _streamsJson = """{"items":[{"tenantId":"t1","domain":"Orders","aggregateId":"o1","lastEventSequence":5,"lastActivityUtc":"2026-01-01T00:00:00Z","eventCount":5,"hasSnapshot":false,"streamStatus":0}],"totalCount":1,"continuationToken":null}""";
    private static readonly string _timelineJson = """{"items":[{"sequenceNumber":1,"timestamp":"2026-01-01T00:00:00Z","entryType":1,"typeName":"OrderPlaced","correlationId":"c1","userId":null}],"totalCount":1,"continuationToken":null}""";
    private static readonly string _stateJson = """{"tenantId":"t1","domain":"Orders","aggregateId":"o1","sequenceNumber":3,"timestamp":"2026-01-01T00:00:00Z","stateJson":"{}"}""";
    private static readonly string _eventDetailJson = """{"tenantId":"t1","domain":"Orders","aggregateId":"o1","sequenceNumber":1,"eventTypeName":"OrderPlaced","timestamp":"2026-01-01T00:00:00Z","correlationId":"c1","causationId":null,"userId":null,"payloadJson":"{}"}""";

    [Fact]
    public async Task ListStreams_ReturnsValidJson_OnSuccess()
    {
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, _streamsJson);
        var client = new AdminApiClient(httpClient);

        string result = await StreamTools.ListStreams(client);

        using JsonDocument doc = JsonDocument.Parse(result);
        doc.RootElement.TryGetProperty("error", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task ListStreams_ReturnsUnreachableError_OnConnectionFailure()
    {
        using HttpClient httpClient = MockHttpMessageHandler.CreateThrowingClient(new HttpRequestException("Connection refused"));
        var client = new AdminApiClient(httpClient);

        string result = await StreamTools.ListStreams(client);

        using JsonDocument doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("adminApiStatus").GetString().ShouldBe("unreachable");
    }

    [Fact]
    public async Task ListStreams_ReturnsUnauthorizedError_OnHttp401()
    {
        using HttpClient httpClient = MockHttpMessageHandler.CreateThrowingClient(
            new HttpRequestException("Unauthorized", null, HttpStatusCode.Unauthorized));
        var client = new AdminApiClient(httpClient);

        string result = await StreamTools.ListStreams(client);

        using JsonDocument doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("adminApiStatus").GetString().ShouldBe("unauthorized");
    }

    [Fact]
    public async Task GetStreamEvents_ReturnsValidJson_OnSuccess()
    {
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, _timelineJson);
        var client = new AdminApiClient(httpClient);

        string result = await StreamTools.GetStreamEvents(client, "t1", "Orders", "o1");

        using JsonDocument doc = JsonDocument.Parse(result);
        doc.RootElement.TryGetProperty("error", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task GetStreamState_ReturnsValidJson_OnSuccess()
    {
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, _stateJson);
        var client = new AdminApiClient(httpClient);

        string result = await StreamTools.GetStreamState(client, "t1", "Orders", "o1", 3);

        using JsonDocument doc = JsonDocument.Parse(result);
        doc.RootElement.TryGetProperty("error", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task GetStreamState_ReturnsNotFound_WhenNull()
    {
        // Return 200 with JSON null body — GetFromJsonAsync returns null
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, "null");
        var client = new AdminApiClient(httpClient);

        string result = await StreamTools.GetStreamState(client, "t1", "Orders", "o1", 999);

        using JsonDocument doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("adminApiStatus").GetString().ShouldBe("not-found");
    }

    [Fact]
    public async Task GetEventDetail_ReturnsValidJson_OnSuccess()
    {
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, _eventDetailJson);
        var client = new AdminApiClient(httpClient);

        string result = await StreamTools.GetEventDetail(client, "t1", "Orders", "o1", 1);

        using JsonDocument doc = JsonDocument.Parse(result);
        doc.RootElement.TryGetProperty("error", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task GetEventDetail_ReturnsNotFound_WhenNull()
    {
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, "null");
        var client = new AdminApiClient(httpClient);

        string result = await StreamTools.GetEventDetail(client, "t1", "Orders", "o1", 999);

        using JsonDocument doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("adminApiStatus").GetString().ShouldBe("not-found");
    }

    [Fact]
    public async Task AllStreamTools_ReturnParseableJson()
    {
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, _streamsJson);
        var client = new AdminApiClient(httpClient);

        string result = await StreamTools.ListStreams(client);

        Should.NotThrow(() => JsonDocument.Parse(result));
    }
}
