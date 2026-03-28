
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
/// Tier 3 end-to-end tests for the command validation endpoint (POST /api/v1/commands/validate).
/// AC: #5, #6, #7, #12.
/// </summary>
[Trait("Category", "E2E")]
[Trait("Tier", "3")]
[Collection("AspireContractTests")]
public class CommandValidationE2ETests {
    private readonly AspireContractTestFixture _fixture;

    public CommandValidationE2ETests(AspireContractTestFixture fixture) => _fixture = fixture;

    /// <summary>
    /// AC #5: Authorized user gets PreflightValidationResult(true).
    /// </summary>
    [Fact]
    public async Task ValidateCommand_AuthorizedUser_Returns200WithIsAuthorizedTrue() {
        // Arrange
        using HttpRequestMessage request = ContractTestHelpers.CreateCommandValidationRequest(
            "tenant-a", "counter", "IncrementCounter");

        // Act
        using HttpResponseMessage response = await _fixture.EventStoreClient
            .SendAsync(request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        JsonElement result = await response.Content.ReadFromJsonAsync<JsonElement>();
        result.GetProperty("isAuthorized").GetBoolean().ShouldBeTrue();
    }

    /// <summary>
    /// AC #6: Wrong tenant claims returns PreflightValidationResult(false, reason) with HTTP 200.
    /// CRITICAL: HTTP status is 200, NOT 403.
    /// </summary>
    [Fact]
    public async Task ValidateCommand_WrongTenant_Returns200WithIsAuthorizedFalse() {
        // Arrange - JWT authorizes tenant-b, request targets tenant-a
        string token = TestJwtTokenGenerator.GenerateToken(
            tenants: ["tenant-b"],
            domains: ["counter"],
            permissions: ["command:submit"]);

        var body = new {
            Tenant = "tenant-a",
            Domain = "counter",
            CommandType = "IncrementCounter",
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/commands/validate") {
            Content = new StringContent(
                JsonSerializer.Serialize(body),
                Encoding.UTF8,
                "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        using HttpResponseMessage response = await _fixture.EventStoreClient
            .SendAsync(request);

        // Assert - HTTP 200 OK, NOT 403
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        JsonElement result = await response.Content.ReadFromJsonAsync<JsonElement>();
        result.GetProperty("isAuthorized").GetBoolean().ShouldBeFalse();
        result.GetProperty("reason").GetString().ShouldNotBeNullOrEmpty();
    }

    /// <summary>
    /// AC #7: No JWT returns 401 Unauthorized.
    /// </summary>
    [Fact]
    public async Task ValidateCommand_NoJwtToken_Returns401Unauthorized() {
        // Arrange - no Authorization header
        var body = new {
            Tenant = "tenant-a",
            Domain = "counter",
            CommandType = "IncrementCounter",
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/commands/validate") {
            Content = new StringContent(
                JsonSerializer.Serialize(body),
                Encoding.UTF8,
                "application/json"),
        };

        // Act
        using HttpResponseMessage response = await _fixture.EventStoreClient
            .SendAsync(request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// AC #12: Missing required fields returns 400 with ProblemDetails.
    /// </summary>
    [Fact]
    public async Task ValidateCommand_MissingRequiredFields_Returns400WithProblemDetails() {
        // Arrange - empty body
        string token = TestJwtTokenGenerator.GenerateToken(
            tenants: ["tenant-a"],
            domains: ["counter"],
            permissions: ["command:submit"]);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/commands/validate") {
            Content = new StringContent(
                "{}",
                Encoding.UTF8,
                "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        using HttpResponseMessage response = await _fixture.EventStoreClient
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
    /// Verify that optional AggregateId doesn't break validation.
    /// </summary>
    [Fact]
    public async Task ValidateCommand_WithOptionalAggregateId_Returns200() {
        // Arrange
        using HttpRequestMessage request = ContractTestHelpers.CreateCommandValidationRequest(
            "tenant-a", "counter", "IncrementCounter", aggregateId: "order-123");

        // Act
        using HttpResponseMessage response = await _fixture.EventStoreClient
            .SendAsync(request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        JsonElement result = await response.Content.ReadFromJsonAsync<JsonElement>();
        result.GetProperty("isAuthorized").GetBoolean().ShouldBeTrue();
    }
}
