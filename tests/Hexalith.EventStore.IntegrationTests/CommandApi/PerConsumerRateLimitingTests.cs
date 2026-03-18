extern alias commandapi;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using commandapi::Hexalith.EventStore.CommandApi.Configuration;

using Hexalith.EventStore.IntegrationTests.Helpers;

using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

using Shouldly;

namespace Hexalith.EventStore.IntegrationTests.CommandApi;

/// <summary>
/// Tests per-consumer rate limiting (Story 7.3, AC #1).
/// Validates that consumers identified by JWT "sub" claim are individually rate-limited
/// alongside the existing per-tenant rate limiting.
/// </summary>
public class PerConsumerRateLimitingTests(PerConsumerRateLimitingWebApplicationFactory factory)
    : IClassFixture<PerConsumerRateLimitingWebApplicationFactory> {
    /// <summary>
    /// Task 5.1: ValidateRateLimitingOptions rejects ConsumerPermitLimit &lt;= 0.
    /// </summary>
    [Fact]
    public void ValidateRateLimitingOptions_RejectsInvalidConsumerPermitLimit() {
        // Arrange
        var options = new RateLimitingOptions { ConsumerPermitLimit = 0 };
        var validator = new ValidateRateLimitingOptions();

        // Act
        ValidateOptionsResult result = validator.Validate(null, options);

        // Assert
        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("ConsumerPermitLimit");
    }

    /// <summary>
    /// Task 5.2: ValidateRateLimitingOptions rejects ConsumerWindowSeconds &lt;= 0.
    /// </summary>
    [Fact]
    public void ValidateRateLimitingOptions_RejectsInvalidConsumerWindowSeconds() {
        // Arrange
        var options = new RateLimitingOptions { ConsumerWindowSeconds = 0 };
        var validator = new ValidateRateLimitingOptions();

        // Act
        ValidateOptionsResult result = validator.Validate(null, options);

        // Assert
        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("ConsumerWindowSeconds");
    }

    /// <summary>
    /// Task 5.3: ValidateRateLimitingOptions rejects ConsumerSegmentsPerWindow &lt; 1.
    /// </summary>
    [Fact]
    public void ValidateRateLimitingOptions_RejectsInvalidConsumerSegmentsPerWindow() {
        // Arrange
        var options = new RateLimitingOptions { ConsumerSegmentsPerWindow = 0 };
        var validator = new ValidateRateLimitingOptions();

        // Act
        ValidateOptionsResult result = validator.Validate(null, options);

        // Assert
        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("ConsumerSegmentsPerWindow");
    }

    /// <summary>
    /// Task 5.4: ValidateRateLimitingOptions rejects ConsumerPermitLimits values &lt;= 0.
    /// </summary>
    [Fact]
    public void ValidateRateLimitingOptions_RejectsInvalidConsumerPermitLimitsValues() {
        // Arrange
        var options = new RateLimitingOptions {
            ConsumerPermitLimits = new Dictionary<string, int> { ["bad-consumer"] = -1 },
        };
        var validator = new ValidateRateLimitingOptions();

        // Act
        ValidateOptionsResult result = validator.Validate(null, options);

        // Assert
        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("bad-consumer");
    }

    /// <summary>
    /// Task 5.5: ValidateRateLimitingOptions accepts valid consumer options.
    /// </summary>
    [Fact]
    public void ValidateRateLimitingOptions_AcceptsValidConsumerOptions() {
        // Arrange
        var options = new RateLimitingOptions {
            ConsumerPermitLimit = 100,
            ConsumerWindowSeconds = 1,
            ConsumerSegmentsPerWindow = 1,
            ConsumerPermitLimits = new Dictionary<string, int> {
                ["service-a"] = 500,
                ["service-b"] = 10000,
            },
        };
        var validator = new ValidateRateLimitingOptions();

        // Act
        ValidateOptionsResult result = validator.Validate(null, options);

        // Assert
        result.Succeeded.ShouldBeTrue();
    }

    /// <summary>
    /// Task 6.2: Same consumer exceeds per-consumer limit → 429.
    /// </summary>
    [Fact]
    public async Task SameConsumer_ExceedsLimit_Returns429() {
        // Arrange - ConsumerPermitLimit=2
        HttpClient client = CreateAuthenticatedClient("consumer-a", "tenant-6-2");

        // Act - send 3 requests
        _ = await client.PostAsJsonAsync("/api/v1/commands", CreateValidRequest("tenant-6-2"));
        _ = await client.PostAsJsonAsync("/api/v1/commands", CreateValidRequest("tenant-6-2"));
        HttpResponseMessage r3 = await client.PostAsJsonAsync("/api/v1/commands", CreateValidRequest("tenant-6-2"));

        // Assert - 3rd request exceeds consumer limit
        r3.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }

    /// <summary>
    /// Task 6.3: Different consumers (different "sub" claims, same tenant) → independent per-consumer limits.
    /// </summary>
    [Fact]
    public async Task DifferentConsumers_SameTenant_IndependentLimits() {
        // Arrange - ConsumerPermitLimit=2
        HttpClient clientA = CreateAuthenticatedClient("consumer-b1", "tenant-6-3");
        HttpClient clientB = CreateAuthenticatedClient("consumer-b2", "tenant-6-3");

        // Act - consumer A sends 2 requests (at limit), consumer B sends 1 request
        HttpResponseMessage consumerAFirst = await clientA.PostAsJsonAsync("/api/v1/commands", CreateValidRequest("tenant-6-3"));
        HttpResponseMessage consumerASecond = await clientA.PostAsJsonAsync("/api/v1/commands", CreateValidRequest("tenant-6-3"));
        HttpResponseMessage consumerBFirst = await clientB.PostAsJsonAsync("/api/v1/commands", CreateValidRequest("tenant-6-3"));

        // Assert - consumer B's first request should NOT be rate limited despite consumer A being at limit
        consumerAFirst.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        consumerASecond.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        consumerBFirst.StatusCode.ShouldBe(HttpStatusCode.Accepted,
            "Different consumers should have independent rate limits.");
    }

    /// <summary>
    /// Task 6.4: Per-consumer override applies (consumer in ConsumerPermitLimits gets higher limit).
    /// </summary>
    [Fact]
    public async Task PremiumConsumer_WithOverride_UsesOverrideThreshold() {
        // Arrange - "premium-consumer" has ConsumerPermitLimit=4 override
        HttpClient client = CreateAuthenticatedClient("premium-consumer", "tenant-6-4");

        // Act - send 5 requests (first 4 within override, 5th exceeds)
        HttpResponseMessage r1 = await client.PostAsJsonAsync("/api/v1/commands", CreateValidRequest("tenant-6-4"));
        HttpResponseMessage r2 = await client.PostAsJsonAsync("/api/v1/commands", CreateValidRequest("tenant-6-4"));
        HttpResponseMessage r3 = await client.PostAsJsonAsync("/api/v1/commands", CreateValidRequest("tenant-6-4"));
        HttpResponseMessage r4 = await client.PostAsJsonAsync("/api/v1/commands", CreateValidRequest("tenant-6-4"));
        HttpResponseMessage r5 = await client.PostAsJsonAsync("/api/v1/commands", CreateValidRequest("tenant-6-4"));

        // Assert
        r1.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        r2.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        r3.StatusCode.ShouldBe(HttpStatusCode.Accepted,
            "Premium consumer should not be rate limited at 3 requests because the override limit is 4.");
        r4.StatusCode.ShouldBe(HttpStatusCode.Accepted,
            "Premium consumer should not be rate limited at 4 requests because the override limit is 4.");
        r5.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests,
            "Premium consumer should be rate limited on the 5th request after the override threshold is exhausted.");
    }

    /// <summary>
    /// Task 6.5: 429 response includes consumerId in ProblemDetails extensions.
    /// </summary>
    [Fact]
    public async Task RateLimited_Response_IncludesConsumerIdInProblemDetails() {
        // Arrange
        HttpClient client = CreateAuthenticatedClient("consumer-c", "tenant-6-5");

        // Act - exceed the limit
        _ = await client.PostAsJsonAsync("/api/v1/commands", CreateValidRequest("tenant-6-5"));
        _ = await client.PostAsJsonAsync("/api/v1/commands", CreateValidRequest("tenant-6-5"));
        HttpResponseMessage r3 = await client.PostAsJsonAsync("/api/v1/commands", CreateValidRequest("tenant-6-5"));

        // Assert
        r3.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        string body = await r3.Content.ReadAsStringAsync();
        JsonDocument doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("consumerId").GetString().ShouldBe("consumer-c");
    }

    /// <summary>
    /// Task 6.6: Health endpoints remain exempted from per-consumer limiting.
    /// </summary>
    [Theory]
    [InlineData("/health")]
    [InlineData("/alive")]
    [InlineData("/ready")]
    public async Task HealthEndpoints_ExemptFromConsumerLimiting(string path) {
        // Arrange - use an unauthenticated client (health endpoints bypass auth)
        HttpClient client = factory.CreateClient();

        // Act - send many requests (more than ConsumerPermitLimit=2)
        HttpResponseMessage r1 = await client.GetAsync(path);
        HttpResponseMessage r2 = await client.GetAsync(path);
        HttpResponseMessage r3 = await client.GetAsync(path);
        HttpResponseMessage r4 = await client.GetAsync(path);
        HttpResponseMessage r5 = await client.GetAsync(path);

        // Assert - health endpoints stay healthy and bypass rate limiting
        r1.StatusCode.ShouldBe(HttpStatusCode.OK);
        r2.StatusCode.ShouldBe(HttpStatusCode.OK);
        r3.StatusCode.ShouldBe(HttpStatusCode.OK);
        r4.StatusCode.ShouldBe(HttpStatusCode.OK);
        r5.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>
    /// Task 6.7: When consumer hits per-consumer limit, 429 response includes valid Retry-After header
    /// and both tenantId and consumerId in ProblemDetails extensions.
    /// </summary>
    [Fact]
    public async Task RateLimited_Response_IncludesRetryAfterAndBothIds() {
        // Arrange
        HttpClient client = CreateAuthenticatedClient("consumer-d", "tenant-6-7");

        // Act - exceed the limit
        _ = await client.PostAsJsonAsync("/api/v1/commands", CreateValidRequest("tenant-6-7"));
        _ = await client.PostAsJsonAsync("/api/v1/commands", CreateValidRequest("tenant-6-7"));
        HttpResponseMessage r3 = await client.PostAsJsonAsync("/api/v1/commands", CreateValidRequest("tenant-6-7"));

        // Assert
        r3.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);

        // Retry-After header must be present
        r3.Headers.RetryAfter.ShouldNotBeNull();

        // ProblemDetails must contain both tenantId and consumerId
        string body = await r3.Content.ReadAsStringAsync();
        JsonDocument doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("tenantId").GetString().ShouldNotBeNullOrEmpty();
        doc.RootElement.GetProperty("consumerId").GetString().ShouldBe("consumer-d");
    }

    /// <summary>
    /// Validates CreateChained ordering: tenant limiter rejects first when the tenant threshold is lower.
    /// </summary>
    [Fact]
    public async Task TenantLimiter_RejectsFirst_WhenTenantLimitIsExceeded() {
        using var tenantFirstFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?> {
                ["EventStore:RateLimiting:PermitLimit"] = "1",
                ["EventStore:RateLimiting:WindowSeconds"] = "60",
                ["EventStore:RateLimiting:SegmentsPerWindow"] = "1",
                ["EventStore:RateLimiting:ConsumerPermitLimit"] = "100",
                ["EventStore:RateLimiting:ConsumerWindowSeconds"] = "60",
                ["EventStore:RateLimiting:ConsumerSegmentsPerWindow"] = "1",
            })));

        HttpClient client = CreateAuthenticatedClient(
            tenantFirstFactory.CreateClient(),
            "consumer-tenant-first",
            "tenant-tenant-first");

        HttpResponseMessage r1 = await client.PostAsJsonAsync("/api/v1/commands", CreateValidRequest("tenant-tenant-first"));
        HttpResponseMessage r2 = await client.PostAsJsonAsync("/api/v1/commands", CreateValidRequest("tenant-tenant-first"));

        r1.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        r2.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        r2.Headers.RetryAfter.ShouldNotBeNull();
        r2.Headers.RetryAfter!.Delta.ShouldBe(TimeSpan.FromSeconds(60));

        JsonDocument doc = JsonDocument.Parse(await r2.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("tenantId").GetString().ShouldBe("tenant-tenant-first");
        doc.RootElement.GetProperty("consumerId").GetString().ShouldBe("consumer-tenant-first");
    }

    /// <summary>
    /// Task 6.8: Anonymous consumer partition — unauthenticated request to a non-health endpoint
    /// falls back to "anonymous" consumer partition key.
    /// </summary>
    [Fact]
    public async Task AnonymousConsumer_FallsBackToAnonymousPartition() {
        // Arrange - unauthenticated client (no bearer token)
        HttpClient client = factory.CreateClient();

        // Act - unauthenticated requests share the anonymous consumer bucket.
        // Authorization challenges the first two requests, but the third request is rejected by the limiter
        // before authorization because UseRateLimiter() runs before UseAuthorization().
        HttpResponseMessage r1 = await client.PostAsJsonAsync("/api/v1/commands", CreateValidRequest("any"));
        HttpResponseMessage r2 = await client.PostAsJsonAsync("/api/v1/commands", CreateValidRequest("any"));
        HttpResponseMessage r3 = await client.PostAsJsonAsync("/api/v1/commands", CreateValidRequest("any"));

        r1.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        r2.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        r3.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests,
            "Unauthenticated requests should share the anonymous consumer partition and hit the limiter on the third request.");

        string body = await r3.Content.ReadAsStringAsync();
        JsonDocument doc = JsonDocument.Parse(body);
        doc.RootElement.TryGetProperty("consumerId", out JsonElement consumerIdElement).ShouldBeTrue();
        consumerIdElement.GetString().ShouldBe("anonymous",
            "Unauthenticated requests should use 'anonymous' consumer partition.");
    }

    private static object CreateValidRequest(string tenant) => new {
        messageId = Guid.NewGuid().ToString(),
        tenant,
        domain = "test-domain",
        aggregateId = $"agg-{Guid.NewGuid():N}",
        commandType = "TestCommand",
        payload = new { value = 1 },
    };

    private HttpClient CreateAuthenticatedClient(string consumerId, string tenant) {
        HttpClient client = factory.CreateClient();
        return CreateAuthenticatedClient(client, consumerId, tenant);
    }

    private static HttpClient CreateAuthenticatedClient(HttpClient client, string consumerId, string tenant) {
        string token = TestJwtTokenGenerator.GenerateToken(
            subject: consumerId,
            tenants: [tenant],
            domains: ["test-domain"]);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
