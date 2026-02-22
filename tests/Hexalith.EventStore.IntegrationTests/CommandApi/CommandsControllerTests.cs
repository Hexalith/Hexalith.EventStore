extern alias commandapi;

namespace Hexalith.EventStore.IntegrationTests.CommandApi;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;

using commandapi::Hexalith.EventStore.CommandApi.Models;

using Hexalith.EventStore.IntegrationTests.Helpers;

using Shouldly;

public class CommandsControllerTests(JwtAuthenticatedWebApplicationFactory factory)
    : IClassFixture<JwtAuthenticatedWebApplicationFactory> {
    private readonly HttpClient _client = CreateAuthenticatedClient(factory);

    [Fact]
    public async Task PostCommands_ValidRequest_Returns202WithCorrelationIdAndHeaders() {
        // Arrange
        var request = new {
            tenant = "test-tenant",
            domain = "test-domain",
            aggregateId = "agg-001",
            commandType = "CreateOrder",
            payload = new { amount = 100 },
        };

        // Act
        HttpResponseMessage response = await _client.PostAsJsonAsync("/api/v1/commands", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        SubmitCommandResponse? body = await response.Content.ReadFromJsonAsync<SubmitCommandResponse>();
        body.ShouldNotBeNull();
        body.CorrelationId.ShouldNotBeNullOrEmpty();
        Guid.TryParse(body.CorrelationId, out _).ShouldBeTrue();

        response.Headers.Location.ShouldNotBeNull();
        response.Headers.Location!.ToString().ShouldContain($"/api/v1/commands/status/{body.CorrelationId}");

        response.Headers.GetValues("Retry-After").ShouldContain("1");
    }

    [Fact]
    public async Task PostCommands_MissingRequiredFields_Returns400ProblemDetails() {
        // Arrange - missing all required fields
        var request = new { };

        // Act
        HttpResponseMessage response = await _client.PostAsJsonAsync("/api/v1/commands", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType!.ShouldContain("problem+json");

        // Verify RFC 7807 ProblemDetails structure
        var problemDetails = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        problemDetails.TryGetProperty("type", out var typeProperty).ShouldBeTrue();
        typeProperty.GetString().ShouldNotBeNullOrEmpty();

        problemDetails.TryGetProperty("title", out var titleProperty).ShouldBeTrue();
        titleProperty.GetString().ShouldNotBeNullOrEmpty();

        problemDetails.TryGetProperty("status", out var statusProperty).ShouldBeTrue();
        statusProperty.GetInt32().ShouldBe(400);
    }

    [Fact]
    public async Task PostCommands_MalformedJson_Returns400ProblemDetails() {
        // Arrange
        using var content = new StringContent("{ not valid json }", Encoding.UTF8, "application/json");

        // Act
        HttpResponseMessage response = await _client.PostAsync("/api/v1/commands", content);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostCommands_WithValidExtensions_Returns202() {
        // Arrange
        var request = new {
            tenant = "test-tenant",
            domain = "test-domain",
            aggregateId = "agg-002",
            commandType = "UpdateOrder",
            payload = new { status = "confirmed" },
            extensions = new Dictionary<string, string> {
                ["requestId"] = "req-123",
                ["source"] = "mobile-app",
            },
        };

        // Act
        HttpResponseMessage response = await _client.PostAsJsonAsync("/api/v1/commands", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        SubmitCommandResponse? body = await response.Content.ReadFromJsonAsync<SubmitCommandResponse>();
        body.ShouldNotBeNull();
        body.CorrelationId.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task PostCommands_WithTooManyExtensions_Returns400() {
        // Arrange - create 51 extension entries (exceeds 50 limit)
        var extensions = Enumerable.Range(1, 51).ToDictionary(i => $"key{i}", i => $"value{i}");
        var request = new {
            tenant = "test-tenant",
            domain = "test-domain",
            aggregateId = "agg-003",
            commandType = "CreateOrder",
            payload = new { amount = 200 },
            extensions,
        };

        // Act
        HttpResponseMessage response = await _client.PostAsJsonAsync("/api/v1/commands", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType!.ShouldContain("problem+json");
    }

    [Fact]
    public async Task PostCommands_WithEmptyTenant_Returns400WithValidationErrors() {
        // Arrange - valid JSON structure but empty tenant (triggers FluentValidation)
        var request = new {
            tenant = "",
            domain = "test-domain",
            aggregateId = "agg-004",
            commandType = "CreateOrder",
            payload = new { amount = 300 },
        };

        // Act
        HttpResponseMessage response = await _client.PostAsJsonAsync("/api/v1/commands", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType!.ShouldContain("problem+json");

        // Verify this is a FluentValidation error with proper structure
        var problemDetails = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        problemDetails.GetProperty("title").GetString().ShouldBe("Validation Failed");
        problemDetails.GetProperty("correlationId").GetString().ShouldNotBeNullOrEmpty();
        problemDetails.GetProperty("validationErrors").GetArrayLength().ShouldBeGreaterThan(0);
    }

    private static HttpClient CreateAuthenticatedClient(JwtAuthenticatedWebApplicationFactory factory) {
        HttpClient client = factory.CreateClient();
        string token = TestJwtTokenGenerator.GenerateToken(tenants: ["test-tenant"], domains: ["test-domain"]);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
