
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;

using Hexalith.EventStore.Testing.Http;

using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.EventStore.Admin.Mcp.Tests;

public class AdminApiClientTests {
    [Fact]
    public void CreatePrimaryHttpMessageHandler_DisablesAutomaticRedirects() {
        using HttpMessageHandler handler = AdminApiClient.CreatePrimaryHttpMessageHandler();

        HttpClientHandler primaryHandler = handler.ShouldBeOfType<HttpClientHandler>();
        primaryHandler.AllowAutoRedirect.ShouldBeFalse();
    }

    [Fact]
    public async Task ProgramRegistration_DoesNotFollowConfirmedWriteRedirects()
    {
        int sourcePort = GetAvailablePort();
        int untrustedPort = GetAvailablePort();
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{sourcePort}/");
        listener.Start();
        Task responder = Task.Run(async () =>
        {
            HttpListenerContext context = await listener.GetContextAsync();
            context.Response.StatusCode = (int)HttpStatusCode.TemporaryRedirect;
            context.Response.RedirectLocation = $"http://127.0.0.1:{untrustedPort}/relay";
            context.Response.Close();
        });
        var services = new ServiceCollection();
        global::Program.AddAdminApiClient(
            services,
            new Uri($"http://127.0.0.1:{sourcePort}"),
            Guid.NewGuid().ToString("N"));
        using ServiceProvider provider = services.BuildServiceProvider();
        AdminApiClient client = provider.GetRequiredService<AdminApiClient>();

        HttpRequestException exception = await Should.ThrowAsync<HttpRequestException>(() => client.PostAsync(
            "/api/v1/admin/projections/orders/reset",
            TestContext.Current.CancellationToken));
        await responder.WaitAsync(TestContext.Current.CancellationToken);

        exception.StatusCode.ShouldBe(HttpStatusCode.TemporaryRedirect);
    }

    [Fact]
    public async Task GetSystemHealthAsync_SendsGetToCorrectPath() {
        // Arrange
        Uri? capturedUri = null;
        using HttpClient httpClient = CreateMockHttpClient(
            (request, _) => {
                capturedUri = request.RequestUri;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) {
                    Content = new StringContent(GetHealthJson(), System.Text.Encoding.UTF8, "application/json"),
                });
            },
            "https://localhost:5443");

        var client = new AdminApiClient(httpClient);

        // Act
        _ = await client.GetSystemHealthAsync(CancellationToken.None);

        // Assert
        _ = capturedUri.ShouldNotBeNull();
        capturedUri.PathAndQuery.ShouldBe("/api/v1/admin/health");
    }

    [Fact]
    public async Task GetSystemHealthAsync_SendsAcceptJsonHeader() {
        // Arrange
        MediaTypeWithQualityHeaderValue? capturedAccept = null;
        using HttpClient httpClient = CreateMockHttpClient(
            (request, _) => {
                capturedAccept = request.Headers.Accept.FirstOrDefault();
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) {
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
        _ = capturedAccept.ShouldNotBeNull();
        capturedAccept.MediaType.ShouldBe("application/json");
    }

    [Fact]
    public async Task GetSystemHealthAsync_SendsAuthorizationBearerHeader() {
        // Arrange
        string token = Guid.NewGuid().ToString("N");
        AuthenticationHeaderValue? capturedAuth = null;
        using HttpClient httpClient = CreateMockHttpClient(
            (request, _) => {
                capturedAuth = request.Headers.Authorization;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) {
                    Content = new StringContent(GetHealthJson(), System.Text.Encoding.UTF8, "application/json"),
                });
            },
            "https://localhost:5443",
            configureDefaults: client =>
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token));

        var client = new AdminApiClient(httpClient);

        // Act
        _ = await client.GetSystemHealthAsync(CancellationToken.None);

        // Assert
        _ = capturedAuth.ShouldNotBeNull();
        capturedAuth.Scheme.ShouldBe("Bearer");
        capturedAuth.Parameter.ShouldBe(token);
    }

    private static HttpClient CreateMockHttpClient(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler,
        string baseAddress,
        Action<HttpClient>? configureDefaults = null) {
        var mockHandler = new MockHttpMessageHandler(handler);
        var client = new HttpClient(mockHandler) {
            BaseAddress = new Uri(baseAddress),
        };
        configureDefaults?.Invoke(client);
        return client;
    }

    private static int GetAvailablePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
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
}
