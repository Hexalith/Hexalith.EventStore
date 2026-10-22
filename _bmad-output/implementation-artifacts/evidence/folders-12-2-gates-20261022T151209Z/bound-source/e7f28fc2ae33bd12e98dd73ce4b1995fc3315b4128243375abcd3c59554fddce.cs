extern alias eventstore;

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace Hexalith.EventStore.IntegrationTests.Helpers;

/// <summary>
/// WebApplicationFactory configured with per-consumer rate limit overrides for testing Story 7.3.
/// Uses long window (60s) with SegmentsPerWindow=1 for test stability — short windows (1s) cause flaky CI tests.
/// </summary>
public class PerConsumerRateLimitingWebApplicationFactory : JwtAuthenticatedWebApplicationFactory {
    protected override void ConfigureWebHost(IWebHostBuilder builder) {
        base.ConfigureWebHost(builder);
        _ = builder.ConfigureAppConfiguration(config => config.AddInMemoryCollection(new Dictionary<string, string?> {
            // Set high tenant limit so per-tenant limiter doesn't interfere
            ["EventStore:RateLimiting:PermitLimit"] = "1000",
            // Per-consumer: low limit for testing, long window for stability
            ["EventStore:RateLimiting:ConsumerPermitLimit"] = "2",
            ["EventStore:RateLimiting:ConsumerWindowSeconds"] = "60",
            ["EventStore:RateLimiting:ConsumerSegmentsPerWindow"] = "1",
            // Per-consumer override for testing
            ["EventStore:RateLimiting:ConsumerPermitLimits:premium-consumer"] = "4",
        }));
    }
}
