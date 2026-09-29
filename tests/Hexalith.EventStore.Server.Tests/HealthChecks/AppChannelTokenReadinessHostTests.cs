extern alias eventstore;

using Hexalith.EventStore.Authentication;
using Hexalith.EventStore.HealthChecks;
using Hexalith.EventStore.Server.Tests.TestUtilities;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;

using Shouldly;

using EventStoreProgram = eventstore::Program;

namespace Hexalith.EventStore.Server.Tests.HealthChecks;

/// <summary>
/// Verifies the app-channel token readiness check in the real Production host, where
/// <c>AllowedCallers</c> is bound only to the named <c>DaprInternal</c> options instance.
/// </summary>
public sealed class AppChannelTokenReadinessHostTests
{
    private const string AuthorityIssuer = "https://identity.example.test/realms/hexalith";

    /// <summary>
    /// Readiness fails without <c>APP_API_TOKEN</c> only while internal callers are allow-listed.
    /// </summary>
    /// <param name="allowListed">Whether an internal caller is allow-listed.</param>
    /// <param name="expected">The expected readiness status.</param>
    [Theory]
    [InlineData(true, HealthStatus.Unhealthy)]
    [InlineData(false, HealthStatus.Healthy)]
    public async Task ProductionReadinessRequiresTokenOnlyForAllowListedCallers(bool allowListed, HealthStatus expected)
    {
        var settings = new Dictionary<string, string?>
        {
            ["Authentication:JwtBearer:Authority"] = AuthorityIssuer,
            ["Authentication:JwtBearer:Issuer"] = AuthorityIssuer,
            ["Authentication:JwtBearer:Audience"] = "hexalith-eventstore",
            ["Authentication:JwtBearer:AllowedAlgorithms:0"] = SecurityAlgorithms.RsaSha256,
            ["Authentication:JwtBearer:SigningKey"] = null,
            ["Authentication:JwtBearer:RequireHttpsMetadata"] = "true",
            [DaprAppChannelTokenValidator.ConfigurationKey] = null,
        };
        if (allowListed)
        {
            settings["Authentication:DaprInternal:AllowedCallers:0"] = "reactor";
        }

        await using WebApplicationFactory<EventStoreProgram> factory = new WebApplicationFactory<EventStoreProgram>()
            .WithWebHostBuilder(builder =>
            {
                _ = builder.UseEnvironment(Environments.Production);
                _ = builder.ConfigureAppConfiguration((_, configuration) =>
                    configuration.AddInMemoryCollection(settings));
                builder.ConfigureTestServices(WebApplicationFactoryServiceOverrides.RemoveAdminOperationalIndexHostedService);
            });
        HealthCheckService health = factory.Services.GetRequiredService<HealthCheckService>();

        HealthReport report = await health.CheckHealthAsync(
            registration => registration.Name == HealthCheckBuilderExtensions.AppChannelTokenHealthCheckName,
            TestContext.Current.CancellationToken);

        report.Entries.Keys.ShouldBe([HealthCheckBuilderExtensions.AppChannelTokenHealthCheckName]);
        report.Status.ShouldBe(expected);
    }
}
