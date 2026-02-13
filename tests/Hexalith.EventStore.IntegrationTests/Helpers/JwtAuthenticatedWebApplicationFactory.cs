extern alias commandapi;

namespace Hexalith.EventStore.IntegrationTests.Helpers;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

using CommandApiProgram = commandapi::Program;

/// <summary>
/// WebApplicationFactory configured with test JWT symmetric key authentication.
/// Used by all integration tests that need authenticated requests.
/// </summary>
public class JwtAuthenticatedWebApplicationFactory : WebApplicationFactory<CommandApiProgram>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ConfigureAppConfiguration(config =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:JwtBearer:Issuer"] = TestJwtTokenGenerator.Issuer,
                ["Authentication:JwtBearer:Audience"] = TestJwtTokenGenerator.Audience,
                ["Authentication:JwtBearer:SigningKey"] = TestJwtTokenGenerator.SigningKey,
                ["Authentication:JwtBearer:RequireHttpsMetadata"] = "false",
            });
        });
    }
}
