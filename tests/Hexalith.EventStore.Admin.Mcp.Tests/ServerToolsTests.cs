
using System.Net;
using System.Text.Json;

using Hexalith.EventStore.Admin.Mcp.Tools;
using Hexalith.EventStore.Testing.Http;

namespace Hexalith.EventStore.Admin.Mcp.Tests;

public class ServerToolsTests {
    [Fact]
    public async Task Ping_ReturnsReachable_WhenApiIsHealthy() {
        // Arrange
        using HttpClient httpClient = CreateMockHttpClient(HttpStatusCode.OK, GetHealthJson());
        var client = new AdminApiClient(httpClient);

        // Act
        string result = await ServerTools.Ping(client, CancellationToken.None);

        // Assert
        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("adminApiStatus").GetString().ShouldBe("reachable");
        doc.RootElement.GetProperty("serverName").GetString().ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Ping_ReturnsUnreachable_WhenConnectionFails() {
        // Arrange
        using HttpClient httpClient = CreateThrowingHttpClient(new HttpRequestException("Connection refused"));
        var client = new AdminApiClient(httpClient);

        // Act
        string result = await ServerTools.Ping(client, CancellationToken.None);

        // Assert
        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("adminApiStatus").GetString().ShouldBe("unreachable");
        doc.RootElement.GetProperty("serverName").GetString().ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Ping_ReturnsUnauthorized_OnHttp401() {
        // Arrange
        using HttpClient httpClient = CreateThrowingHttpClient(
            new HttpRequestException("Unauthorized", null, HttpStatusCode.Unauthorized));
        var client = new AdminApiClient(httpClient);

        // Act
        string result = await ServerTools.Ping(client, CancellationToken.None);

        // Assert
        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("adminApiStatus").GetString().ShouldBe("unauthorized");
        doc.RootElement.GetProperty("serverName").GetString().ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Ping_ReturnsError_OnHttp500() {
        // Arrange
        using HttpClient httpClient = CreateThrowingHttpClient(
            new HttpRequestException("Internal Server Error", null, HttpStatusCode.InternalServerError));
        var client = new AdminApiClient(httpClient);

        // Act
        string result = await ServerTools.Ping(client, CancellationToken.None);

        // Assert
        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("adminApiStatus").GetString().ShouldBe("error");
        doc.RootElement.GetProperty("serverName").GetString().ShouldNotBeNullOrWhiteSpace();
        doc.RootElement.GetProperty("details").GetString()!.ShouldContain("500");
    }

    [Fact]
    public async Task Ping_ReturnsUnreachable_OnTimeout() {
        // Arrange
        using HttpClient httpClient = CreateThrowingHttpClient(new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout."));
        var client = new AdminApiClient(httpClient);

        // Act
        string result = await ServerTools.Ping(client, CancellationToken.None);

        // Assert
        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("adminApiStatus").GetString().ShouldBe("unreachable");
        doc.RootElement.GetProperty("serverName").GetString().ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Ping_ReturnsError_OnMalformedJson() {
        // Arrange
        using HttpClient httpClient = CreateMockHttpClient(HttpStatusCode.OK, "<html>not json</html>");
        var client = new AdminApiClient(httpClient);

        // Act
        string result = await ServerTools.Ping(client, CancellationToken.None);

        // Assert
        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("adminApiStatus").GetString().ShouldBe("error");
        doc.RootElement.GetProperty("serverName").GetString().ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Ping_AlwaysReturnsServerNameAndAdminApiStatus() {
        // Arrange
        using HttpClient httpClient = CreateMockHttpClient(HttpStatusCode.OK, GetHealthJson());
        var client = new AdminApiClient(httpClient);

        // Act
        string result = await ServerTools.Ping(client, CancellationToken.None);

        // Assert
        using var doc = JsonDocument.Parse(result);
        doc.RootElement.TryGetProperty("serverName", out _).ShouldBeTrue();
        doc.RootElement.TryGetProperty("adminApiStatus", out _).ShouldBeTrue();
        doc.RootElement.TryGetProperty("serverVersion", out _).ShouldBeTrue();
    }

    private static HttpClient CreateMockHttpClient(HttpStatusCode statusCode, string content)
        => MockHttpMessageHandler.CreateJsonClient(statusCode, content);

    private static HttpClient CreateThrowingHttpClient(Exception exception)
        => MockHttpMessageHandler.CreateThrowingClient(exception);

    private static string GetHealthJson()
        => """
        {
            "overallStatus": 0,
            "totalEventCount": 100,
            "eventsPerSecond": 5.0,
            "errorPercentage": 0.1,
            "daprComponents": [],
            "observabilityLinks": { "traceUrl": null, "metricsUrl": null, "logsUrl": null }
        }
        """;
}
