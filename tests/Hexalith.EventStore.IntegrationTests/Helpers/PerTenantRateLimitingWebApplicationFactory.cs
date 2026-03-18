extern alias commandapi;

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace Hexalith.EventStore.IntegrationTests.Helpers;

/// <summary>
/// WebApplicationFactory configured with per-tenant rate limit overrides for testing Story 7.2.
/// Default PermitLimit=2 (same as base), with "premium" tenant override of 4.
/// </summary>
public class PerTenantRateLimitingWebApplicationFactory : JwtAuthenticatedWebApplicationFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        _ = builder.ConfigureAppConfiguration(config => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["EventStore:RateLimiting:PermitLimit"] = "2",
            ["EventStore:RateLimiting:WindowSeconds"] = "60",
            ["EventStore:RateLimiting:SegmentsPerWindow"] = "1",
            // Per-tenant overrides: "premium" and "premium-429" get PermitLimit=4
            ["EventStore:RateLimiting:TenantPermitLimits:premium"] = "4",
            ["EventStore:RateLimiting:TenantPermitLimits:premium-429"] = "4",
        }));
    }
}
