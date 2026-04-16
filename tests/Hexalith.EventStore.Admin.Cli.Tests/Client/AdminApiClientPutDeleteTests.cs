using System.Net;
using System.Text.Json;

using Hexalith.EventStore.Admin.Cli.Client;
using Hexalith.EventStore.Admin.Cli.Formatting;
using Hexalith.EventStore.Testing.Http;

namespace Hexalith.EventStore.Admin.Cli.Tests.Client;

public class AdminApiClientPutDeleteTests {
    private record TestResponse(string Id, bool Success);

    [Fact]
    public async Task PutAsync_Success_ReturnsDeserializedResponse() {
        // Arrange
        TestResponse expectedResponse = new("put-1", true);
        MockHttpMessageHandler handler = new(new HttpResponseMessage(HttpStatusCode.OK) {
            Content = new StringContent(
                JsonSerializer.Serialize(expectedResponse, JsonDefaults.Options),
                System.Text.Encoding.UTF8,
                "application/json"),
        });
        GlobalOptions options = new("http://localhost:5002", null, "json", null);
        using AdminApiClient client = new(options, handler);

        // Act
        TestResponse result = await client.PutAsync<TestResponse>("/test", CancellationToken.None);

        // Assert
        _ = result.ShouldNotBeNull();
        result.Id.ShouldBe("put-1");
        result.Success.ShouldBeTrue();
        _ = handler.LastRequest.ShouldNotBeNull();
        handler.LastRequest.Method.ShouldBe(HttpMethod.Put);
    }

    [Fact]
    public async Task PutAsync_403_ThrowsAdminApiException() {
        // Arrange
        MockHttpMessageHandler handler = new(new HttpResponseMessage(HttpStatusCode.Forbidden));
        GlobalOptions options = new("http://localhost:5002", null, "json", null);
        using AdminApiClient client = new(options, handler);

        // Act & Assert
        AdminApiException ex = await Should.ThrowAsync<AdminApiException>(
            () => client.PutAsync<TestResponse>("/test", CancellationToken.None));
        ex.Message.ShouldContain("Access denied");
        ex.HttpStatusCode.ShouldBe(403);
    }

    [Fact]
    public async Task PutAsync_ConnectionRefused_ThrowsAdminApiException() {
        // Arrange
        MockHttpMessageHandler handler = new(_ =>
            throw new HttpRequestException("Connection refused", new System.Net.Sockets.SocketException()));
        GlobalOptions options = new("http://localhost:5002", null, "json", null);
        using AdminApiClient client = new(options, handler);

        // Act & Assert
        AdminApiException ex = await Should.ThrowAsync<AdminApiException>(
            () => client.PutAsync<TestResponse>("/test", CancellationToken.None));
        ex.Message.ShouldContain("Cannot connect");
    }

    [Fact]
    public async Task DeleteAsync_Success_ReturnsDeserializedResponse() {
        // Arrange
        TestResponse expectedResponse = new("del-1", true);
        MockHttpMessageHandler handler = new(new HttpResponseMessage(HttpStatusCode.OK) {
            Content = new StringContent(
                JsonSerializer.Serialize(expectedResponse, JsonDefaults.Options),
                System.Text.Encoding.UTF8,
                "application/json"),
        });
        GlobalOptions options = new("http://localhost:5002", null, "json", null);
        using AdminApiClient client = new(options, handler);

        // Act
        TestResponse result = await client.DeleteAsync<TestResponse>("/test", CancellationToken.None);

        // Assert
        _ = result.ShouldNotBeNull();
        result.Id.ShouldBe("del-1");
        result.Success.ShouldBeTrue();
        _ = handler.LastRequest.ShouldNotBeNull();
        handler.LastRequest.Method.ShouldBe(HttpMethod.Delete);
    }

    [Fact]
    public async Task DeleteAsync_404_ThrowsAdminApiException() {
        // Arrange
        MockHttpMessageHandler handler = new(new HttpResponseMessage(HttpStatusCode.NotFound));
        GlobalOptions options = new("http://localhost:5002", null, "json", null);
        using AdminApiClient client = new(options, handler);

        // Act & Assert
        AdminApiException ex = await Should.ThrowAsync<AdminApiException>(
            () => client.DeleteAsync<TestResponse>("/test", CancellationToken.None));
        ex.Message.ShouldContain("not found");
        ex.HttpStatusCode.ShouldBe(404);
    }

    [Fact]
    public async Task DeleteAsync_ConnectionRefused_ThrowsAdminApiException() {
        // Arrange
        MockHttpMessageHandler handler = new(_ =>
            throw new HttpRequestException("Connection refused", new System.Net.Sockets.SocketException()));
        GlobalOptions options = new("http://localhost:5002", null, "json", null);
        using AdminApiClient client = new(options, handler);

        // Act & Assert
        AdminApiException ex = await Should.ThrowAsync<AdminApiException>(
            () => client.DeleteAsync<TestResponse>("/test", CancellationToken.None));
        ex.Message.ShouldContain("Cannot connect");
    }
}
