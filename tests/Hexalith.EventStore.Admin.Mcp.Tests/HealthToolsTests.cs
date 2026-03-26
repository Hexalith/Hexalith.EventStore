namespace Hexalith.EventStore.Admin.Mcp.Tests;

using System.Net;
using System.Text.Json;

using Hexalith.EventStore.Admin.Mcp.Tests.TestHelpers;
using Hexalith.EventStore.Admin.Mcp.Tools;

public class HealthToolsTests
{
    private static readonly string _healthJson = """{"overallStatus":0,"totalEventCount":100,"eventsPerSecond":5.0,"errorPercentage":0.1,"daprComponents":[],"observabilityLinks":{"traceUrl":null,"metricsUrl":null,"logsUrl":null}}""";
    private static readonly string _daprJson = """[{"componentName":"statestore","componentType":"state.redis","status":0,"lastCheckUtc":"2026-01-01T00:00:00Z"}]""";

    [Fact]
    public async Task GetHealthStatus_ReturnsValidJson_OnSuccess()
    {
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, _healthJson);
        var client = new AdminApiClient(httpClient);

        string result = await ServerTools.GetHealthStatus(client, CancellationToken.None);

        using JsonDocument doc = JsonDocument.Parse(result);
        doc.RootElement.TryGetProperty("error", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task GetHealthStatus_ReturnsErrorJson_OnFailure()
    {
        using HttpClient httpClient = MockHttpMessageHandler.CreateThrowingClient(new HttpRequestException("Connection refused"));
        var client = new AdminApiClient(httpClient);

        string result = await ServerTools.GetHealthStatus(client, CancellationToken.None);

        using JsonDocument doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("adminApiStatus").GetString().ShouldBe("unreachable");
    }

    [Fact]
    public async Task GetDaprHealth_ReturnsValidJson_OnSuccess()
    {
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, _daprJson);
        var client = new AdminApiClient(httpClient);

        string result = await ServerTools.GetDaprHealth(client, CancellationToken.None);

        using JsonDocument doc = JsonDocument.Parse(result);
        doc.RootElement.GetArrayLength().ShouldBe(1);
    }

    [Fact]
    public async Task GetDaprHealth_ReturnsErrorJson_OnFailure()
    {
        using HttpClient httpClient = MockHttpMessageHandler.CreateThrowingClient(new HttpRequestException("Connection refused"));
        var client = new AdminApiClient(httpClient);

        string result = await ServerTools.GetDaprHealth(client, CancellationToken.None);

        using JsonDocument doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("adminApiStatus").GetString().ShouldBe("unreachable");
    }

    [Fact]
    public async Task GetHealthStatus_ReturnsTimeout_OnTaskCanceled()
    {
        using HttpClient httpClient = MockHttpMessageHandler.CreateThrowingClient(new TaskCanceledException("The request was canceled"));
        var client = new AdminApiClient(httpClient);

        string result = await ServerTools.GetHealthStatus(client, CancellationToken.None);

        using JsonDocument doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("adminApiStatus").GetString().ShouldBe("timeout");
    }
}
