extern alias commandapi;

using System.Net;
using System.Text.Json;

using Hexalith.EventStore.IntegrationTests.Helpers;

using Shouldly;

namespace Hexalith.EventStore.IntegrationTests.CommandApi;

public class OpenApiIntegrationTests(JwtAuthenticatedWebApplicationFactory factory)
    : IClassFixture<JwtAuthenticatedWebApplicationFactory> {
    [Fact]
    public async Task GetOpenApiDocument_Returns200WithJson() {
        // Arrange
        HttpClient client = factory.CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync("/openapi/v1.json");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType!.ShouldContain("json");
    }

    [Fact]
    public async Task GetOpenApiDocument_ContainsCommandsEndpoint() {
        // Arrange
        HttpClient client = factory.CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync("/openapi/v1.json");
        string content = await response.Content.ReadAsStringAsync();
        JsonElement doc = JsonSerializer.Deserialize<JsonElement>(content);

        // Assert
        doc.TryGetProperty("paths", out JsonElement paths).ShouldBeTrue();
        paths.TryGetProperty("/api/v1/commands", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task GetOpenApiDocument_ContainsStatusEndpoint() {
        // Arrange
        HttpClient client = factory.CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync("/openapi/v1.json");
        string content = await response.Content.ReadAsStringAsync();
        JsonElement doc = JsonSerializer.Deserialize<JsonElement>(content);

        // Assert
        doc.TryGetProperty("paths", out JsonElement paths).ShouldBeTrue();
        // OpenAPI path parameters use {param} syntax
        string statusPath = "/api/v1/commands/status/{correlationId}";
        paths.TryGetProperty(statusPath, out _).ShouldBeTrue(
            $"Expected path '{statusPath}' in paths: {string.Join(", ", EnumeratePropertyNames(paths))}");
    }

    [Fact]
    public async Task GetOpenApiDocument_ContainsReplayEndpoint() {
        // Arrange
        HttpClient client = factory.CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync("/openapi/v1.json");
        string content = await response.Content.ReadAsStringAsync();
        JsonElement doc = JsonSerializer.Deserialize<JsonElement>(content);

        // Assert
        doc.TryGetProperty("paths", out JsonElement paths).ShouldBeTrue();
        string replayPath = "/api/v1/commands/replay/{correlationId}";
        paths.TryGetProperty(replayPath, out _).ShouldBeTrue(
            $"Expected path '{replayPath}' in paths: {string.Join(", ", EnumeratePropertyNames(paths))}");
    }

    [Fact]
    public async Task GetOpenApiDocument_ContainsSecurityScheme() {
        // Arrange
        HttpClient client = factory.CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync("/openapi/v1.json");
        string content = await response.Content.ReadAsStringAsync();
        JsonElement doc = JsonSerializer.Deserialize<JsonElement>(content);

        // Assert
        doc.TryGetProperty("components", out JsonElement components).ShouldBeTrue();
        components.TryGetProperty("securitySchemes", out JsonElement schemes).ShouldBeTrue();
        schemes.TryGetProperty("Bearer", out JsonElement bearer).ShouldBeTrue();
        bearer.GetProperty("type").GetString().ShouldBe("http");
        bearer.GetProperty("scheme").GetString().ShouldBe("bearer");
    }

    [Fact]
    public async Task GetSwaggerUI_Returns200() {
        // Arrange
        HttpClient client = factory.CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync("/swagger/index.html");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType!.ShouldContain("text/html");
    }

    [Fact]
    public async Task GetOpenApiDocument_IsValidOpenApi() {
        // Arrange
        HttpClient client = factory.CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync("/openapi/v1.json");
        string content = await response.Content.ReadAsStringAsync();
        JsonElement doc = JsonSerializer.Deserialize<JsonElement>(content);

        // Assert - verify it has the openapi version field and essential structure
        doc.TryGetProperty("openapi", out JsonElement version).ShouldBeTrue();
        version.GetString().ShouldStartWith("3.");
        doc.TryGetProperty("info", out _).ShouldBeTrue();
        doc.TryGetProperty("paths", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task GetOpenApiDocument_Contains429Response() {
        // Arrange
        HttpClient client = factory.CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync("/openapi/v1.json");
        string content = await response.Content.ReadAsStringAsync();
        JsonElement doc = JsonSerializer.Deserialize<JsonElement>(content);

        // Assert - verify 429 response is documented on the commands POST endpoint
        JsonElement paths = doc.GetProperty("paths");
        JsonElement commandsPost = paths.GetProperty("/api/v1/commands").GetProperty("post");
        JsonElement responses = commandsPost.GetProperty("responses");
        responses.TryGetProperty("429", out _).ShouldBeTrue("Expected 429 response documented on POST /api/v1/commands");
    }

    private static IEnumerable<string> EnumeratePropertyNames(JsonElement element) {
        foreach (JsonProperty property in element.EnumerateObject()) {
            yield return property.Name;
        }
    }
}
