namespace Hexalith.EventStore.Admin.Mcp.Tests;

using System.Net;
using System.Net.Http.Headers;

public class AdminApiClientTests
{
    [Fact]
    public async Task GetSystemHealthAsync_SendsGetToCorrectPath()
    {
        // Arrange
        Uri? capturedUri = null;
        using HttpClient httpClient = CreateMockHttpClient(
            (request, _) =>
            {
                capturedUri = request.RequestUri;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(GetHealthJson(), System.Text.Encoding.UTF8, "application/json"),
                });
            },
            "https://localhost:5443");

        var client = new AdminApiClient(httpClient);

        // Act
        _ = await client.GetSystemHealthAsync(CancellationToken.None);

        // Assert
        capturedUri.ShouldNotBeNull();
        capturedUri.PathAndQuery.ShouldBe("/api/v1/admin/health");
    }

    [Fact]
    public async Task GetSystemHealthAsync_SendsAcceptJsonHeader()
    {
        // Arrange
        MediaTypeWithQualityHeaderValue? capturedAccept = null;
        using HttpClient httpClient = CreateMockHttpClient(
            (request, _) =>
            {
                capturedAccept = request.Headers.Accept.FirstOrDefault();
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(GetHealthJson(), System.Text.Encoding.UTF8, "application/json"),
                });
            },
            "https://localhost:5443",
            configureDefaults: client =>
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json")));

        var client = new AdminApiClient(httpClient);

        // Act
        _ = await client.GetSystemHealthAsync(CancellationToken.None);

        // Assert
        capturedAccept.ShouldNotBeNull();
        capturedAccept.MediaType.ShouldBe("application/json");
    }

    [Fact]
    public async Task GetSystemHealthAsync_SendsAuthorizationBearerHeader()
    {
        // Arrange
        AuthenticationHeaderValue? capturedAuth = null;
        using HttpClient httpClient = CreateMockHttpClient(
            (request, _) =>
            {
                capturedAuth = request.Headers.Authorization;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(GetHealthJson(), System.Text.Encoding.UTF8, "application/json"),
                });
            },
            "https://localhost:5443",
            configureDefaults: client =>
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "test-token"));

        var client = new AdminApiClient(httpClient);

        // Act
        _ = await client.GetSystemHealthAsync(CancellationToken.None);

        // Assert
        capturedAuth.ShouldNotBeNull();
        capturedAuth.Scheme.ShouldBe("Bearer");
        capturedAuth.Parameter.ShouldBe("test-token");
    }

    private static HttpClient CreateMockHttpClient(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler,
        string baseAddress,
        Action<HttpClient>? configureDefaults = null)
    {
        var mockHandler = new MockHttpMessageHandler(handler);
        var client = new HttpClient(mockHandler)
        {
            BaseAddress = new Uri(baseAddress),
        };
        configureDefaults?.Invoke(client);
        return client;
    }

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

    private sealed class MockHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => _handler(request, cancellationToken);
    }
}
