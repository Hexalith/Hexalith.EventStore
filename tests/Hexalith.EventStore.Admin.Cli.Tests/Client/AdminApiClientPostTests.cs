using System.Net;
using System.Text.Json;

using Hexalith.EventStore.Admin.Cli.Client;
using Hexalith.EventStore.Admin.Cli.Formatting;
using Hexalith.EventStore.Testing.Http;

namespace Hexalith.EventStore.Admin.Cli.Tests.Client;

public class AdminApiClientPostTests {
    private record TestResponse(string Id, bool Success);

    [Fact]
    public async Task AdminApiClient_PostAsync_SendsJsonBody() {
        CancellationToken ct = TestContext.Current.CancellationToken;

        // Arrange
        TestResponse expectedResponse = new("test-1", true);
        MockHttpMessageHandler handler = new(new HttpResponseMessage(HttpStatusCode.OK) {
            Content = new StringContent(
                JsonSerializer.Serialize(expectedResponse, JsonDefaults.Options),
                System.Text.Encoding.UTF8,
                "application/json"),
        });
        GlobalOptions options = new("http://localhost:5002", null, "json", null);
        using AdminApiClient client = new(options, handler);

        // Act
        TestResponse result = await client.PostAsync<TestResponse>("/test", new { Name = "test" }, ct);

        // Assert
        _ = result.ShouldNotBeNull();
        result.Id.ShouldBe("test-1");
        _ = handler.LastRequest.ShouldNotBeNull();
        handler.LastRequest.Method.ShouldBe(HttpMethod.Post);
        _ = handler.LastRequest.Content.ShouldNotBeNull();
        string body = await handler.LastRequest.Content!.ReadAsStringAsync(ct);
        body.ShouldContain("\"name\"");
        body.ShouldContain("\"test\"");
        handler.LastRequest.Content!.Headers.ContentType!.MediaType.ShouldBe("application/json");
    }

    [Fact]
    public async Task AdminApiClient_PostAsync_Handles202AsSuccess() {
        CancellationToken ct = TestContext.Current.CancellationToken;

        // Arrange
        TestResponse expectedResponse = new("op-1", true);
        MockHttpMessageHandler handler = new(new HttpResponseMessage(HttpStatusCode.Accepted) {
            Content = new StringContent(
                JsonSerializer.Serialize(expectedResponse, JsonDefaults.Options),
                System.Text.Encoding.UTF8,
                "application/json"),
        });
        GlobalOptions options = new("http://localhost:5002", null, "json", null);
        using AdminApiClient client = new(options, handler);

        // Act
        TestResponse result = await client.PostAsync<TestResponse>("/test", ct);

        // Assert
        _ = result.ShouldNotBeNull();
        result.Id.ShouldBe("op-1");
        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task AdminApiClient_PostAsync_EmptyBody_SendsEmptyJson() {
        CancellationToken ct = TestContext.Current.CancellationToken;

        // Arrange
        TestResponse expectedResponse = new("op-2", true);
        MockHttpMessageHandler handler = new(new HttpResponseMessage(HttpStatusCode.OK) {
            Content = new StringContent(
                JsonSerializer.Serialize(expectedResponse, JsonDefaults.Options),
                System.Text.Encoding.UTF8,
                "application/json"),
        });
        GlobalOptions options = new("http://localhost:5002", null, "json", null);
        using AdminApiClient client = new(options, handler);

        // Act
        TestResponse result = await client.PostAsync<TestResponse>("/test", ct);

        // Assert
        _ = result.ShouldNotBeNull();
        _ = handler.LastRequest.ShouldNotBeNull();
        _ = handler.LastRequest.Content.ShouldNotBeNull();
        string body = await handler.LastRequest.Content!.ReadAsStringAsync(ct);
        body.ShouldBe("{}");
    }

    [Fact]
    public async Task AdminApiClient_PostAsync_Http404_ThrowsApiException() {
        CancellationToken ct = TestContext.Current.CancellationToken;

        // Arrange
        MockHttpMessageHandler handler = new(new HttpResponseMessage(HttpStatusCode.NotFound));
        GlobalOptions options = new("http://localhost:5002", null, "json", null);
        using AdminApiClient client = new(options, handler);

        // Act & Assert
        AdminApiException ex = await Should.ThrowAsync<AdminApiException>(
            () => client.PostAsync<TestResponse>("/api/v1/admin/projections/acme/counter-view/pause", ct));
        ex.Message.ShouldContain("Resource not found");
        ex.HttpStatusCode.ShouldBe(404);
    }

    [Fact]
    public async Task AdminApiClient_PostAsync_Http403_ThrowsAccessDenied() {
        CancellationToken ct = TestContext.Current.CancellationToken;

        // Arrange
        MockHttpMessageHandler handler = new(new HttpResponseMessage(HttpStatusCode.Forbidden));
        GlobalOptions options = new("http://localhost:5002", null, "json", null);
        using AdminApiClient client = new(options, handler);

        // Act & Assert
        AdminApiException ex = await Should.ThrowAsync<AdminApiException>(
            () => client.PostAsync<TestResponse>("/test", ct));
        ex.Message.ShouldContain("Access denied");
        ex.HttpStatusCode.ShouldBe(403);
    }

    [Fact]
    public async Task AdminApiClient_PostAsync_ConnectionRefused_ThrowsConnectError() {
        CancellationToken ct = TestContext.Current.CancellationToken;

        // Arrange
        MockHttpMessageHandler handler = new(_ =>
            throw new HttpRequestException("Connection refused", new System.Net.Sockets.SocketException()));
        GlobalOptions options = new("http://localhost:5002", null, "json", null);
        using AdminApiClient client = new(options, handler);

        // Act & Assert
        AdminApiException ex = await Should.ThrowAsync<AdminApiException>(
            () => client.PostAsync<TestResponse>("/test", ct));
        ex.Message.ShouldContain("Cannot connect");
    }
}
