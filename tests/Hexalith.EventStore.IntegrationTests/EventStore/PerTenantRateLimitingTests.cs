extern alias eventstore;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

using eventstore::Hexalith.EventStore.Configuration;

using Hexalith.EventStore.IntegrationTests.Helpers;

using Microsoft.Extensions.Options;

using Shouldly;

namespace Hexalith.EventStore.IntegrationTests.EventStore;

/// <summary>
/// Tests per-tenant rate limit overrides (Story 7.2, AC #1).
/// Validates that tenants with TenantPermitLimits overrides get their specific limit,
/// tenants without overrides get the default PermitLimit, and that the validator rejects invalid values.
/// </summary>
public class PerTenantRateLimitingTests(PerTenantRateLimitingWebApplicationFactory factory)
    : IClassFixture<PerTenantRateLimitingWebApplicationFactory> {
    /// <summary>
    /// Task 4.1: Tenant with TenantPermitLimits override gets the override limit (not the default).
    /// The "premium" tenant has PermitLimit=4 (vs default of 2), so its first 4 requests must not be rate limited.
    /// </summary>
    [Fact]
    public async Task PremiumTenant_WithOverride_UsesOverrideThreshold() {
        // Arrange
        HttpClient client = CreateAuthenticatedClient("premium");

        // Act - send 5 requests (first 4 within override, 5th exceeds it)
        HttpResponseMessage r1 = await client.PostAsJsonAsync("/api/v1/commands", CreateValidRequest("premium"));
        HttpResponseMessage r2 = await client.PostAsJsonAsync("/api/v1/commands", CreateValidRequest("premium"));
        HttpResponseMessage r3 = await client.PostAsJsonAsync("/api/v1/commands", CreateValidRequest("premium"));
        HttpResponseMessage r4 = await client.PostAsJsonAsync("/api/v1/commands", CreateValidRequest("premium"));
        HttpResponseMessage r5 = await client.PostAsJsonAsync("/api/v1/commands", CreateValidRequest("premium"));

        // Assert - first 4 should not be throttled by the tenant limiter; 5th should be.
        r1.StatusCode.ShouldNotBe(HttpStatusCode.TooManyRequests);
        r2.StatusCode.ShouldNotBe(HttpStatusCode.TooManyRequests);
        r3.StatusCode.ShouldNotBe(HttpStatusCode.TooManyRequests,
            "Premium tenant should not be rate limited at 3 requests because the override limit is 4.");
        r4.StatusCode.ShouldNotBe(HttpStatusCode.TooManyRequests,
            "Premium tenant should not be rate limited at 4 requests because the override limit is 4.");
        r5.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests,
            "Premium tenant should be rate limited on the 5th request after the override threshold is exhausted.");
    }

    /// <summary>
    /// Task 4.2: Tenant without override gets the default PermitLimit.
    /// A regular tenant (not in TenantPermitLimits) should be rate limited at 2 requests (default).
    /// </summary>
    [Fact]
    public async Task RegularTenant_WithoutOverride_GetsDefaultLimit() {
        // Arrange
        HttpClient client = CreateAuthenticatedClient("regular-tenant");

        // Act - send 3 requests (exceeds default of 2)
        _ = await client.PostAsJsonAsync("/api/v1/commands", CreateValidRequest("regular-tenant"));
        _ = await client.PostAsJsonAsync("/api/v1/commands", CreateValidRequest("regular-tenant"));
        HttpResponseMessage r3 = await client.PostAsJsonAsync("/api/v1/commands", CreateValidRequest("regular-tenant"));

        // Assert - 3rd request should be rate limited (default is 2)
        r3.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }

    /// <summary>
    /// Task 4.3: ValidateRateLimitingOptions rejects per-tenant values &lt;= 0.
    /// </summary>
    [Fact]
    public void ValidateRateLimitingOptions_RejectsNegativeTenantPermitLimit() {
        // Arrange
        var options = new RateLimitingOptions {
            TenantPermitLimits = new Dictionary<string, int> { ["bad-tenant"] = -1 },
        };
        var validator = new ValidateRateLimitingOptions();

        // Act
        ValidateOptionsResult result = validator.Validate(null, options);

        // Assert
        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("bad-tenant");
    }

    /// <summary>
    /// Task 4.3 (continued): ValidateRateLimitingOptions rejects per-tenant values == 0.
    /// </summary>
    [Fact]
    public void ValidateRateLimitingOptions_RejectsZeroTenantPermitLimit() {
        // Arrange
        var options = new RateLimitingOptions {
            TenantPermitLimits = new Dictionary<string, int> { ["zero-tenant"] = 0 },
        };
        var validator = new ValidateRateLimitingOptions();

        // Act
        ValidateOptionsResult result = validator.Validate(null, options);

        // Assert
        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("zero-tenant");
    }

    /// <summary>
    /// Task 4.3 (supplementary): ValidateRateLimitingOptions accepts valid per-tenant values.
    /// </summary>
    [Fact]
    public void ValidateRateLimitingOptions_AcceptsValidTenantPermitLimits() {
        // Arrange
        var options = new RateLimitingOptions {
            TenantPermitLimits = new Dictionary<string, int> {
                ["tenant-a"] = 500,
                ["tenant-b"] = 10000,
            },
        };
        var validator = new ValidateRateLimitingOptions();

        // Act
        ValidateOptionsResult result = validator.Validate(null, options);

        // Assert
        result.Succeeded.ShouldBeTrue();
    }

    private static object CreateValidRequest(string tenant) => new {
        messageId = Guid.NewGuid().ToString(),
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
