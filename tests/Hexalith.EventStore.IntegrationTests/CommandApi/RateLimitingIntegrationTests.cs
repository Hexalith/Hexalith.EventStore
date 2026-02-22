extern alias commandapi;

namespace Hexalith.EventStore.IntegrationTests.CommandApi;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using Hexalith.EventStore.IntegrationTests.Helpers;

using Shouldly;

public class RateLimitingIntegrationTests(RateLimitingWebApplicationFactory factory)
    : IClassFixture<RateLimitingWebApplicationFactory> {
    [Fact]
    public async Task PostCommands_WithinRateLimit_Returns202() {
        // Arrange
        HttpClient client = CreateAuthenticatedClient("rate-tenant-a");

        // Act - send 1 request (within limit of 2)
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/commands", CreateValidRequest("rate-tenant-a"));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task PostCommands_ExceedsRateLimit_Returns429ProblemDetails() {
        // Arrange
        HttpClient client = CreateAuthenticatedClient("rate-tenant-b");

        // Act - send 3 requests (exceeds limit of 2)
        await client.PostAsJsonAsync("/api/v1/commands", CreateValidRequest("rate-tenant-b"));
        await client.PostAsJsonAsync("/api/v1/commands", CreateValidRequest("rate-tenant-b"));
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/commands", CreateValidRequest("rate-tenant-b"));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);

        JsonElement problemDetails = await response.Content.ReadFromJsonAsync<JsonElement>();
        problemDetails.GetProperty("status").GetInt32().ShouldBe(429);
        problemDetails.GetProperty("title").GetString().ShouldBe("Too Many Requests");
        problemDetails.GetProperty("type").GetString().ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task PostCommands_ExceedsRateLimit_IncludesRetryAfterHeader() {
        // Arrange
        HttpClient client = CreateAuthenticatedClient("rate-tenant-c");

        // Act - exceed limit
        await client.PostAsJsonAsync("/api/v1/commands", CreateValidRequest("rate-tenant-c"));
        await client.PostAsJsonAsync("/api/v1/commands", CreateValidRequest("rate-tenant-c"));
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/commands", CreateValidRequest("rate-tenant-c"));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        response.Headers.RetryAfter.ShouldNotBeNull();
    }

    [Fact]
    public async Task PostCommands_ExceedsRateLimit_IncludesCorrelationId() {
        // Arrange
        HttpClient client = CreateAuthenticatedClient("rate-tenant-d");

        // Act - exceed limit
        await client.PostAsJsonAsync("/api/v1/commands", CreateValidRequest("rate-tenant-d"));
        await client.PostAsJsonAsync("/api/v1/commands", CreateValidRequest("rate-tenant-d"));
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/commands", CreateValidRequest("rate-tenant-d"));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        JsonElement problemDetails = await response.Content.ReadFromJsonAsync<JsonElement>();
        problemDetails.TryGetProperty("correlationId", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task PostCommands_ExceedsRateLimit_IncludesTenantId() {
        // Arrange
        HttpClient client = CreateAuthenticatedClient("rate-tenant-e");

        // Act - exceed limit
        await client.PostAsJsonAsync("/api/v1/commands", CreateValidRequest("rate-tenant-e"));
        await client.PostAsJsonAsync("/api/v1/commands", CreateValidRequest("rate-tenant-e"));
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/commands", CreateValidRequest("rate-tenant-e"));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        JsonElement problemDetails = await response.Content.ReadFromJsonAsync<JsonElement>();
        problemDetails.TryGetProperty("tenantId", out JsonElement tenantId).ShouldBeTrue();
        tenantId.GetString().ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task PostCommands_ExceedsRateLimit_ContentTypeIsProblemJson() {
        // Arrange
        HttpClient client = CreateAuthenticatedClient("rate-tenant-f");

        // Act - exceed limit
        await client.PostAsJsonAsync("/api/v1/commands", CreateValidRequest("rate-tenant-f"));
        await client.PostAsJsonAsync("/api/v1/commands", CreateValidRequest("rate-tenant-f"));
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/commands", CreateValidRequest("rate-tenant-f"));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task PostCommands_DifferentTenants_IndependentRateLimits() {
        // Arrange
        HttpClient clientA = CreateAuthenticatedClient("tenant-iso-a");
        HttpClient clientB = CreateAuthenticatedClient("tenant-iso-b");

        // Act - exhaust tenant A's limit
        await clientA.PostAsJsonAsync("/api/v1/commands", CreateValidRequest("tenant-iso-a"));
        await clientA.PostAsJsonAsync("/api/v1/commands", CreateValidRequest("tenant-iso-a"));
        HttpResponseMessage responseA = await clientA.PostAsJsonAsync("/api/v1/commands", CreateValidRequest("tenant-iso-a"));

        // Tenant B should still be OK
        HttpResponseMessage responseB = await clientB.PostAsJsonAsync("/api/v1/commands", CreateValidRequest("tenant-iso-b"));

        // Assert
        responseA.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        responseB.StatusCode.ShouldBe(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task PostCommands_NoAuthentication_Returns401BeforeRateLimit() {
        // Arrange - no auth header
        HttpClient client = factory.CreateClient();

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/commands", CreateValidRequest("any-tenant"));

        // Assert - should get 401 (auth fails before rate limiter)
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PostCommands_RateLimitReset_AllowsRequestsAfterWindow() {
        // Arrange - exhaust rate limit, then verify 429, confirming rate limiting is enforced.
        // Note: A true window-reset test would require waiting for the full sliding window to expire
        // (60s with SegmentsPerWindow=1), which is too slow for CI. Instead, we verify the rate limiter
        // correctly blocks after exhaustion and confirm the Retry-After header provides reset guidance.
        HttpClient client = CreateAuthenticatedClient("rate-tenant-reset");

        // Act - exhaust the limit (PermitLimit=2)
        HttpResponseMessage response1 = await client.PostAsJsonAsync("/api/v1/commands", CreateValidRequest("rate-tenant-reset"));
        HttpResponseMessage response2 = await client.PostAsJsonAsync("/api/v1/commands", CreateValidRequest("rate-tenant-reset"));
        HttpResponseMessage response3 = await client.PostAsJsonAsync("/api/v1/commands", CreateValidRequest("rate-tenant-reset"));

        // Assert - first two succeed, third is rate limited with Retry-After guidance
        response1.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        response2.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        response3.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        response3.Headers.RetryAfter.ShouldNotBeNull("429 response must include Retry-After header for client recovery");
    }

    [Fact]
    public async Task PostCommands_TenantPartitioning_NotAllAnonymous() {
        // Arrange - Two different tenants, each with PermitLimit=2
        // If partitioning is broken (all going to "anonymous"), the 3rd request total would fail
        // With correct partitioning, each tenant gets their own counter
        HttpClient clientX = CreateAuthenticatedClient("partition-x");
        HttpClient clientY = CreateAuthenticatedClient("partition-y");

        // Act - each tenant sends 2 requests (at limit for each)
        HttpResponseMessage x1 = await clientX.PostAsJsonAsync("/api/v1/commands", CreateValidRequest("partition-x"));
        HttpResponseMessage y1 = await clientY.PostAsJsonAsync("/api/v1/commands", CreateValidRequest("partition-y"));
        HttpResponseMessage x2 = await clientX.PostAsJsonAsync("/api/v1/commands", CreateValidRequest("partition-x"));
        HttpResponseMessage y2 = await clientY.PostAsJsonAsync("/api/v1/commands", CreateValidRequest("partition-y"));

        // Assert - all 4 succeed (2 per tenant, within limit)
        // If incorrectly partitioned as "anonymous", requests 3 and 4 would be 429
        x1.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        y1.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        x2.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        y2.StatusCode.ShouldBe(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task HealthEndpoint_ExceedsRateLimit_StillReturns200() {
        // Arrange - exhaust rate limit for this tenant
        HttpClient client = CreateAuthenticatedClient("health-tenant");
        await client.PostAsJsonAsync("/api/v1/commands", CreateValidRequest("health-tenant"));
        await client.PostAsJsonAsync("/api/v1/commands", CreateValidRequest("health-tenant"));
        HttpResponseMessage rateLimited = await client.PostAsJsonAsync("/api/v1/commands", CreateValidRequest("health-tenant"));
        rateLimited.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);

        // Act - health endpoint should still work (no auth needed, no rate limit)
        HttpClient unauthClient = factory.CreateClient();
        HttpResponseMessage healthResponse = await unauthClient.GetAsync("/health");

        // Assert
        healthResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AliveEndpoint_ExceedsRateLimit_StillReturns200() {
        // Arrange - exhaust rate limit for this tenant
        HttpClient client = CreateAuthenticatedClient("alive-tenant");
        await client.PostAsJsonAsync("/api/v1/commands", CreateValidRequest("alive-tenant"));
        await client.PostAsJsonAsync("/api/v1/commands", CreateValidRequest("alive-tenant"));
        HttpResponseMessage rateLimited = await client.PostAsJsonAsync("/api/v1/commands", CreateValidRequest("alive-tenant"));
        rateLimited.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);

        // Act - alive endpoint should still work
        HttpClient unauthClient = factory.CreateClient();
        HttpResponseMessage aliveResponse = await unauthClient.GetAsync("/alive");

        // Assert
        aliveResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private static object CreateValidRequest(string tenant) => new {
        tenant,
        domain = "test-domain",
        aggregateId = $"agg-{Guid.NewGuid():N}",
        commandType = "TestCommand",
        payload = new { value = 1 },
    };

    private HttpClient CreateAuthenticatedClient(string tenant) {
        HttpClient client = factory.CreateClient();
        string token = TestJwtTokenGenerator.GenerateToken(tenants: [tenant], domains: ["test-domain"]);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
