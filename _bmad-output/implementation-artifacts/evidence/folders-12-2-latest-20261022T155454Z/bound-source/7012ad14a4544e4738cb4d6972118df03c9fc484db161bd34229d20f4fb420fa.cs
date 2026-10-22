
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
/// Tier 3 end-to-end tests for the query endpoint (POST /api/v1/queries).
/// AC: #1, #2, #3, #4.
/// </summary>
[Trait("Category", "E2E")]
[Trait("Tier", "3")]
[Collection("AspireContractTests")]
public class QueryEndpointE2ETests {
    private readonly AspireContractTestFixture _fixture;

    public QueryEndpointE2ETests(AspireContractTestFixture fixture) => _fixture = fixture;

    /// <summary>
    /// AC #1: POST /api/v1/queries returns 401 when no JWT is provided.
    /// </summary>
    [Fact]
    public async Task SubmitQuery_NoJwtToken_Returns401Unauthorized() {
        // Arrange - no Authorization header
        var body = new {
            Tenant = "tenant-a",
            Domain = "counter",
            AggregateId = "query-auth-1",
            QueryType = "GetCounterState",
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/queries") {
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
    /// AC #2: POST /api/v1/queries returns 403 when JWT lacks tenant access.
    /// </summary>
    [Fact]
    public async Task SubmitQuery_WrongTenantClaims_Returns403Forbidden() {
        // Arrange - JWT authorizes tenant-b, request targets tenant-a
        string token = TestJwtTokenGenerator.GenerateToken(
            tenants: ["tenant-b"],
            domains: ["counter"],
            permissions: ["query:read"]);

        var body = new {
            Tenant = "tenant-a",
            Domain = "counter",
            AggregateId = "query-tenant-1",
            QueryType = "GetCounterState",
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/queries") {
            Content = new StringContent(
                JsonSerializer.Serialize(body),
                Encoding.UTF8,
                "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        using HttpResponseMessage response = await _fixture.EventStoreClient
            .SendAsync(request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// AC #3: POST /api/v1/queries returns 400 with ProblemDetails when required fields are missing.
    /// </summary>
    [Fact]
    public async Task SubmitQuery_MissingRequiredFields_Returns400WithProblemDetails() {
        // Arrange - empty JSON body
        string token = TestJwtTokenGenerator.GenerateToken(
            tenants: ["tenant-a"],
            domains: ["counter"],
            permissions: ["query:read"]);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/queries") {
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
    /// AC #4: POST /api/v1/queries returns 404 when projection actor type is not registered.
    /// Uses a domain name that will never have a projection actor to future-proof the test.
    /// </summary>
    [Fact]
    public async Task SubmitQuery_ProjectionActorNotRegistered_Returns404() {
        // Arrange - use domain that has no projection actor
        // JWT must include the nonexistent domain in claims so authorization passes
        string token = TestJwtTokenGenerator.GenerateToken(
            tenants: ["tenant-a"],
            domains: ["nonexistent-projection-domain"],
            permissions: ["query:read"]);

        var body = new {
            Tenant = "tenant-a",
            Domain = "nonexistent-projection-domain",
            AggregateId = "query-404-1",
            QueryType = "GetNonexistentState",
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/queries") {
            Content = new StringContent(
                JsonSerializer.Serialize(body),
                Encoding.UTF8,
                "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        using HttpResponseMessage response = await _fixture.EventStoreClient
            .SendAsync(request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        string contentType = response.Content.Headers.ContentType?.MediaType ?? "";
        contentType.ShouldContain("problem+json");

        JsonElement problemDetails = await response.Content.ReadFromJsonAsync<JsonElement>();
        problemDetails.GetProperty("status").GetInt32().ShouldBe(404);
        problemDetails.GetProperty("title").GetString().ShouldNotBeNullOrEmpty();
    }
}
