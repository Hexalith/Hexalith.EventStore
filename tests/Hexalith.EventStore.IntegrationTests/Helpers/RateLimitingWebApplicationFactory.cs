extern alias commandapi;

namespace Hexalith.EventStore.IntegrationTests.Helpers;

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

/// <summary>
/// WebApplicationFactory configured with low rate limits for testing.
/// Overrides the high base limit from JwtAuthenticatedWebApplicationFactory.
/// </summary>
public class RateLimitingWebApplicationFactory : JwtAuthenticatedWebApplicationFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureAppConfiguration(config =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["EventStore:RateLimiting:PermitLimit"] = "2",
                ["EventStore:RateLimiting:WindowSeconds"] = "60",
                ["EventStore:RateLimiting:SegmentsPerWindow"] = "1",
            });
        });
    }
}
