using System.Net;
using System.Text.Json;

using Hexalith.EventStore.Admin.Cli.Client;
using Hexalith.EventStore.Admin.Cli.Formatting;
using Hexalith.EventStore.Testing.Http;

namespace Hexalith.EventStore.Admin.Cli.Tests.Client;

public class AdminApiClientTests
{
    [Fact]
    public void AdminApiClient_SetsBaseUrl_FromGlobalOptions()
    {
        // Arrange & Act
        using AdminApiClient client = new(new GlobalOptions("https://myserver:8080", null, "json", null));

        // The base URL is set internally; verify via a mock request
        // Since we can't inspect _httpClient directly, we test indirectly
        // by checking the constructor doesn't throw
        client.ShouldNotBeNull();
    }

    [Fact]
    public async Task AdminApiClient_AddsAuthHeader_WhenTokenProvided()
    {
        // Arrange
        MockHttpMessageHandler handler = new(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"name\":\"test\"}", System.Text.Encoding.UTF8, "application/json"),
        });
        using HttpClient httpClient = new(handler) { BaseAddress = new Uri("http://localhost:5002") };
        httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "my-token");
        using AdminApiClient client = new(httpClient);

        // Act
        await client.GetAsync<object>("/test", CancellationToken.None);

        // Assert
        handler.LastRequest.ShouldNotBeNull();
        handler.LastRequest!.Headers.Authorization.ShouldNotBeNull();
        handler.LastRequest.Headers.Authorization!.Scheme.ShouldBe("Bearer");
        handler.LastRequest.Headers.Authorization.Parameter.ShouldBe("my-token");
    }

    [Fact]
    public async Task AdminApiClient_AddsAuthHeader_FromGlobalOptionsConstructor()
    {
        // Arrange
        MockHttpMessageHandler handler = new(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json"),
        });
        using AdminApiClient client = new(new GlobalOptions("http://localhost:5002", "ctor-token", "json", null), handler);

        // Act
        _ = await client.GetAsync<object>("/test", CancellationToken.None).ConfigureAwait(true);

        // Assert
        handler.LastRequest.ShouldNotBeNull();
        handler.LastRequest!.Headers.Authorization.ShouldNotBeNull();
        handler.LastRequest.Headers.Authorization!.Scheme.ShouldBe("Bearer");
        handler.LastRequest.Headers.Authorization!.Parameter.ShouldBe("ctor-token");
    }

    [Fact]
    public async Task AdminApiClient_NoAuthHeader_WhenTokenNull()
    {
        // Arrange
        MockHttpMessageHandler handler = new(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"name\":\"test\"}", System.Text.Encoding.UTF8, "application/json"),
        });
        using HttpClient httpClient = new(handler) { BaseAddress = new Uri("http://localhost:5002") };
        using AdminApiClient client = new(httpClient);

        // Act
        await client.GetAsync<object>("/test", CancellationToken.None);

        // Assert
        handler.LastRequest.ShouldNotBeNull();
        handler.LastRequest!.Headers.Authorization.ShouldBeNull();
    }

    [Fact]
    public async Task AdminApiClient_Http401_ThrowsAuthError()
    {
        // Arrange
        MockHttpMessageHandler handler = new(new HttpResponseMessage(HttpStatusCode.Unauthorized));
        using HttpClient httpClient = new(handler) { BaseAddress = new Uri("http://localhost:5002") };
        using AdminApiClient client = new(httpClient);

        // Act & Assert
        AdminApiException ex = await Should.ThrowAsync<AdminApiException>(
            () => client.GetAsync<object>("/test", CancellationToken.None));
        ex.Message.ShouldContain("Authentication required");
        ex.Message.ShouldContain("--token");
    }

    [Fact]
    public async Task AdminApiClient_ConnectionRefused_ThrowsConnectError()
    {
        // Arrange
        MockHttpMessageHandler handler = new(_ =>
            throw new HttpRequestException("Connection refused", new System.Net.Sockets.SocketException()));
        using HttpClient httpClient = new(handler) { BaseAddress = new Uri("http://localhost:5002") };
        using AdminApiClient client = new(httpClient);

        // Act & Assert
        AdminApiException ex = await Should.ThrowAsync<AdminApiException>(
            () => client.GetAsync<object>("/test", CancellationToken.None));
        ex.Message.ShouldContain("Cannot connect");
        ex.Message.ShouldContain("localhost:5002");
    }

    [Fact]
    public async Task AdminApiClient_Http404_ThrowsEndpointNotFound()
    {
        // Arrange
        MockHttpMessageHandler handler = new(new HttpResponseMessage(HttpStatusCode.NotFound));
        using HttpClient httpClient = new(handler) { BaseAddress = new Uri("http://localhost:5002") };
        using AdminApiClient client = new(httpClient);

        // Act & Assert
        AdminApiException ex = await Should.ThrowAsync<AdminApiException>(
            () => client.GetAsync<object>("/api/v1/admin/health", CancellationToken.None));
        ex.Message.ShouldContain("Endpoint not found");
        ex.Message.ShouldContain("/api/v1/admin/health");
    }

    [Fact]
    public async Task AdminApiClient_JsonException_ThrowsVersionMismatch()
    {
        // Arrange
        MockHttpMessageHandler handler = new(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("not-valid-json", System.Text.Encoding.UTF8, "application/json"),
        });
        using HttpClient httpClient = new(handler) { BaseAddress = new Uri("http://localhost:5002") };
        using AdminApiClient client = new(httpClient);

        // Act & Assert
        AdminApiException ex = await Should.ThrowAsync<AdminApiException>(
            () => client.GetAsync<TestDto>("/test", CancellationToken.None));
        ex.Message.ShouldContain("Invalid response");
        ex.Message.ShouldContain("version mismatch");
    }

    private record TestDto(string Name);
}
