extern alias commandapi;

using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Testing.Fakes;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using CommandApiProgram = commandapi::Program;

namespace Hexalith.EventStore.IntegrationTests.Helpers;
/// <summary>
/// WebApplicationFactory configured with test JWT symmetric key authentication.
/// Used by all integration tests that need authenticated requests.
/// Overrides Dapr store registrations with InMemory implementations for tests.
/// </summary>
public class JwtAuthenticatedWebApplicationFactory : WebApplicationFactory<CommandApiProgram> {
    /// <summary>Gets the shared InMemoryCommandStatusStore instance used across all tests.</summary>
    public InMemoryCommandStatusStore StatusStore { get; } = new();

    /// <summary>Gets the shared InMemoryCommandArchiveStore instance used across all tests.</summary>
    public InMemoryCommandArchiveStore ArchiveStore { get; } = new();

    /// <summary>Gets the shared FakeCommandRouter instance used across all tests.</summary>
    public FakeCommandRouter Router { get; } = new() { FakeActor = new FakeAggregateActor() };

    protected override void ConfigureWebHost(IWebHostBuilder builder) {
        ArgumentNullException.ThrowIfNull(builder);
        _ = builder.ConfigureAppConfiguration(config => config.AddInMemoryCollection(new Dictionary<string, string?> {
            ["Authentication:JwtBearer:Issuer"] = TestJwtTokenGenerator.Issuer,
            ["Authentication:JwtBearer:Audience"] = TestJwtTokenGenerator.Audience,
            ["Authentication:JwtBearer:SigningKey"] = TestJwtTokenGenerator.SigningKey,
            ["Authentication:JwtBearer:RequireHttpsMetadata"] = "false",
            // H16: High rate limit to prevent existing tests from hitting rate limits
            ["EventStore:RateLimiting:PermitLimit"] = "10000",
        }));

        _ = builder.ConfigureServices(services => {
            // Remove the DaprCommandStatusStore registration and replace with InMemory
            ServiceDescriptor? statusDescriptor = services.FirstOrDefault(
                d => d.ServiceType == typeof(ICommandStatusStore));
            if (statusDescriptor is not null) {
                _ = services.Remove(statusDescriptor);
            }

            _ = services.AddSingleton<ICommandStatusStore>(StatusStore);

            // Remove the DaprCommandArchiveStore registration and replace with InMemory
            ServiceDescriptor? archiveDescriptor = services.FirstOrDefault(
                d => d.ServiceType == typeof(ICommandArchiveStore));
            if (archiveDescriptor is not null) {
                _ = services.Remove(archiveDescriptor);
            }

            _ = services.AddSingleton<ICommandArchiveStore>(ArchiveStore);

            // Replace CommandRouter with fake (no DAPR actor infrastructure needed)
            TestServiceOverrides.ReplaceCommandRouter(services, Router);

            // Remove Dapr health checks that require a running sidecar
            TestServiceOverrides.RemoveDaprHealthChecks(services);
        });
    }
}
