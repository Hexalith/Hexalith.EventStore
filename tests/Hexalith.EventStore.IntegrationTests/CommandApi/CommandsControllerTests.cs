extern alias commandapi;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using commandapi::Hexalith.EventStore.CommandApi.Models;

using Hexalith.EventStore.IntegrationTests.Helpers;

using Shouldly;

namespace Hexalith.EventStore.IntegrationTests.CommandApi;

public class CommandsControllerTests(JwtAuthenticatedWebApplicationFactory factory)
    : IClassFixture<JwtAuthenticatedWebApplicationFactory> {
    private readonly HttpClient _client = CreateAuthenticatedClient(factory);

    [Fact]
    public async Task PostCommands_ValidRequest_Returns202WithCorrelationIdAndHeaders() {
        // Arrange
        string messageId = Guid.NewGuid().ToString();
        var request = new {
            messageId,
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
        _ = body.ShouldNotBeNull();
        // FR4: CorrelationId defaults to MessageId when not provided
        body.CorrelationId.ShouldBe(messageId);

        // Location header: absolute URI pointing to status endpoint
        _ = response.Headers.Location.ShouldNotBeNull();
        response.Headers.Location!.ToString().ShouldContain($"/api/v1/commands/status/{body.CorrelationId}");

        // Retry-After: delta-seconds format (integer "1")
        response.Headers.GetValues("Retry-After").ShouldContain("1");

        // X-Correlation-ID: middleware-generated HTTP tracing header
        response.Headers.TryGetValues("X-Correlation-ID", out IEnumerable<string>? xcorrelationValues).ShouldBeTrue();
        _ = xcorrelationValues.ShouldNotBeNull();
        xcorrelationValues!.First().ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task PostCommands_NullCorrelationId_DefaultsToMessageId() {
        // Arrange — CorrelationId omitted from JSON (defaults to null in DTO)
        string messageId = Guid.NewGuid().ToString();
        var request = new {
            messageId,
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
        _ = body.ShouldNotBeNull();

        // FR4: CorrelationId defaults to MessageId when not provided
        body.CorrelationId.ShouldBe(messageId);

        // Location header should reference the defaulted correlationId (= messageId)
        _ = response.Headers.Location.ShouldNotBeNull();
        response.Headers.Location!.ToString().ShouldContain($"/api/v1/commands/status/{messageId}");
    }

    [Fact]
    public async Task PostCommands_EmptyCorrelationId_DefaultsToMessageId() {
        // Arrange — CorrelationId is empty string (should default to MessageId per FR4)
        string messageId = Guid.NewGuid().ToString();
        var request = new {
            messageId,
            tenant = "test-tenant",
            domain = "test-domain",
            aggregateId = "agg-001",
            commandType = "CreateOrder",
            payload = new { amount = 100 },
            correlationId = "",
        };

        // Act
        HttpResponseMessage response = await _client.PostAsJsonAsync("/api/v1/commands", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        SubmitCommandResponse? body = await response.Content.ReadFromJsonAsync<SubmitCommandResponse>();
        _ = body.ShouldNotBeNull();

        // FR4: CorrelationId defaults to MessageId when empty
        body.CorrelationId.ShouldBe(messageId);

        // Location header should reference the defaulted correlationId (= messageId)
        _ = response.Headers.Location.ShouldNotBeNull();
        response.Headers.Location!.ToString().ShouldContain($"/api/v1/commands/status/{messageId}");
    }

    [Fact]
    public async Task PostCommands_ExplicitCorrelationId_IsPreserved() {
        // Arrange — explicit CorrelationId provided, should be used as-is
        string messageId = Guid.NewGuid().ToString();
        string explicitCorrelationId = Guid.NewGuid().ToString();
        var request = new {
            messageId,
            tenant = "test-tenant",
            domain = "test-domain",
            aggregateId = "agg-001",
            commandType = "CreateOrder",
            payload = new { amount = 100 },
            correlationId = explicitCorrelationId,
        };

        // Act
        HttpResponseMessage response = await _client.PostAsJsonAsync("/api/v1/commands", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        SubmitCommandResponse? body = await response.Content.ReadFromJsonAsync<SubmitCommandResponse>();
        _ = body.ShouldNotBeNull();

        // Explicit CorrelationId preserved
        body.CorrelationId.ShouldBe(explicitCorrelationId);

        // Location header references the explicit correlationId
        _ = response.Headers.Location.ShouldNotBeNull();
        response.Headers.Location!.ToString().ShouldContain($"/api/v1/commands/status/{explicitCorrelationId}");
    }

    [Fact]
    public async Task PostCommands_LocationHeader_IsAbsoluteUri() {
        // Arrange
        string messageId = Guid.NewGuid().ToString();
        var request = new {
            messageId,
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
        _ = response.Headers.Location.ShouldNotBeNull();

        // Location header uses absolute URI format (RFC 7231 Section 7.1.2)
        response.Headers.Location!.IsAbsoluteUri.ShouldBeTrue();
        response.Headers.Location!.ToString().ShouldStartWith("http");
    }

    [Fact]
    public async Task PostCommands_RetryAfterHeader_UsesDeltaSecondsFormat() {
        // Arrange
        string messageId = Guid.NewGuid().ToString();
        var request = new {
            messageId,
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

        // Retry-After must be delta-seconds (integer "1"), NOT HTTP-date
        IEnumerable<string> retryAfterValues = response.Headers.GetValues("Retry-After");
        string retryAfter = retryAfterValues.Single();
        retryAfter.ShouldBe("1");
        int.TryParse(retryAfter, out int seconds).ShouldBeTrue();
        seconds.ShouldBe(1);
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
        JsonElement problemDetails = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        problemDetails.TryGetProperty("type", out JsonElement typeProperty).ShouldBeTrue();
        typeProperty.GetString().ShouldNotBeNullOrEmpty();

        problemDetails.TryGetProperty("title", out JsonElement titleProperty).ShouldBeTrue();
        titleProperty.GetString().ShouldNotBeNullOrEmpty();

        problemDetails.TryGetProperty("status", out JsonElement statusProperty).ShouldBeTrue();
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
        string messageId = Guid.NewGuid().ToString();
        var request = new {
            messageId,
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
        _ = body.ShouldNotBeNull();
        body.CorrelationId.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task PostCommands_WithTooManyExtensions_Returns400() {
        // Arrange - create 51 extension entries (exceeds 50 limit)
        var extensions = Enumerable.Range(1, 51).ToDictionary(i => $"key{i}", i => $"value{i}");
        string messageId = Guid.NewGuid().ToString();
        var request = new {
            messageId,
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
        string messageId = Guid.NewGuid().ToString();
        var request = new {
            messageId,
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

        // Verify this is a FluentValidation error with proper RFC 7807 structure
        JsonElement problemDetails = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        problemDetails.GetProperty("title").GetString().ShouldBe("Command Validation Failed");
        problemDetails.GetProperty("type").GetString().ShouldBe("https://hexalith.io/problems/validation-error");
        problemDetails.GetProperty("correlationId").GetString().ShouldNotBeNullOrEmpty();
        problemDetails.TryGetProperty("errors", out JsonElement errors).ShouldBeTrue();
        errors.EnumerateObject().Count().ShouldBeGreaterThan(0);
    }

    private static HttpClient CreateAuthenticatedClient(JwtAuthenticatedWebApplicationFactory factory) {
        HttpClient client = factory.CreateClient();
        string token = TestJwtTokenGenerator.GenerateToken(tenants: ["test-tenant"], domains: ["test-domain"]);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
