
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using Hexalith.EventStore.IntegrationTests.Fixtures;
using Hexalith.EventStore.IntegrationTests.Helpers;

using Shouldly;

namespace Hexalith.EventStore.IntegrationTests.ContractTests;

/// <summary>
/// Tier 3 end-to-end tests verifying RFC 7807 ProblemDetails error responses (AC #4).
/// All error responses must return application/problem+json with required fields.
/// </summary>
[Trait("Category", "E2E")]
[Trait("Tier", "3")]
[Collection("AspireContractTests")]
public class ErrorResponseTests {
    private readonly AspireContractTestFixture _fixture;

    public ErrorResponseTests(AspireContractTestFixture fixture) => _fixture = fixture;

    /// <summary>
    /// AC #4: Malformed JSON body returns 400 Bad Request with ProblemDetails.
    /// </summary>
    [Fact]
    public async Task SubmitCommand_MalformedJson_Returns400WithProblemDetails() {
        // Arrange
        string token = TestJwtTokenGenerator.GenerateToken(
            tenants: ["tenant-a"],
            domains: ["counter"],
            permissions: ["command:submit"]);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/commands") {
            Content = new StringContent(
                "{ this is not valid json }",
                Encoding.UTF8,
                "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        using HttpResponseMessage response = await _fixture.CommandApiClient
            .SendAsync(request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        string contentType = response.Content.Headers.ContentType?.MediaType ?? "";
        contentType.ShouldContain("problem+json");

        JsonElement problemDetails = await response.Content.ReadFromJsonAsync<JsonElement>();
        problemDetails.GetProperty("status").GetInt32().ShouldBe(400);
        problemDetails.GetProperty("title").GetString().ShouldNotBeNullOrEmpty();
    }

    /// <summary>
    /// AC #4: Missing required fields returns 400 Bad Request with validation errors in ProblemDetails.
    /// </summary>
    [Fact]
    public async Task SubmitCommand_MissingRequiredFields_Returns400WithProblemDetails() {
        // Arrange - empty object, missing tenant/domain/aggregateId/commandType/payload
        string token = TestJwtTokenGenerator.GenerateToken(
            tenants: ["tenant-a"],
            domains: ["counter"],
            permissions: ["command:submit"]);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/commands") {
            Content = new StringContent(
                "{}",
                Encoding.UTF8,
                "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        using HttpResponseMessage response = await _fixture.CommandApiClient
            .SendAsync(request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        string contentType = response.Content.Headers.ContentType?.MediaType ?? "";
        contentType.ShouldContain("problem+json");

        JsonElement problemDetails = await response.Content.ReadFromJsonAsync<JsonElement>();
        problemDetails.GetProperty("status").GetInt32().ShouldBe(400);
    }

    /// <summary>
    /// AC #4: Unauthorized request returns 401.
    /// </summary>
    [Fact]
    public async Task SubmitCommand_Unauthorized_Returns401() {
        // Arrange - no auth token
        var body = new {
            Tenant = "tenant-a",
            Domain = "counter",
            AggregateId = "err-401",
            CommandType = "IncrementCounter",
            Payload = new { id = "test" },
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/commands") {
            Content = new StringContent(
                JsonSerializer.Serialize(body),
                Encoding.UTF8,
                "application/json"),
        };

        // Act
        using HttpResponseMessage response = await _fixture.CommandApiClient
            .SendAsync(request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// AC #4: Forbidden request (wrong tenant) returns 403 with ProblemDetails including
    /// correlationId and tenantId extension fields.
    /// </summary>
    [Fact]
    public async Task SubmitCommand_Forbidden_Returns403WithProblemDetails() {
        // Arrange - token for tenant-b, request for tenant-a
        string token = TestJwtTokenGenerator.GenerateToken(
            tenants: ["tenant-b"],
            domains: ["counter"],
            permissions: ["command:submit"]);

        var body = new {
            Tenant = "tenant-a",
            Domain = "counter",
            AggregateId = "err-403",
            CommandType = "IncrementCounter",
            Payload = new { id = "test" },
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/commands") {
            Content = new StringContent(
                JsonSerializer.Serialize(body),
                Encoding.UTF8,
                "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        using HttpResponseMessage response = await _fixture.CommandApiClient
            .SendAsync(request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        string contentType = response.Content.Headers.ContentType?.MediaType ?? "";
        contentType.ShouldContain("problem+json");

        JsonElement problemDetails = await response.Content.ReadFromJsonAsync<JsonElement>();
        problemDetails.GetProperty("status").GetInt32().ShouldBe(403);
        problemDetails.TryGetProperty("correlationId", out _).ShouldBeTrue();
        problemDetails.TryGetProperty("tenantId", out _).ShouldBeTrue();
    }

    /// <summary>
    /// AC #4: All error responses include correlationId extension field.
    /// Verify on the status endpoint 404 error response.
    /// </summary>
    [Fact]
    public async Task GetStatus_NotFound_Returns404WithCorrelationIdExtension() {
        // Arrange
        string token = TestJwtTokenGenerator.GenerateToken(
            tenants: ["tenant-a"],
            domains: ["counter"],
            permissions: ["command:submit", "command:query"]);

        string nonExistentId = Guid.NewGuid().ToString();

        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/commands/status/{nonExistentId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        using HttpResponseMessage response = await _fixture.CommandApiClient
            .SendAsync(request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        string contentType = response.Content.Headers.ContentType?.MediaType ?? "";
        contentType.ShouldContain("problem+json");

        JsonElement problemDetails = await response.Content.ReadFromJsonAsync<JsonElement>();
        problemDetails.GetProperty("status").GetInt32().ShouldBe(404);
        problemDetails.TryGetProperty("correlationId", out JsonElement corrIdProp).ShouldBeTrue();
        corrIdProp.GetString().ShouldNotBeNullOrEmpty();
    }
}
