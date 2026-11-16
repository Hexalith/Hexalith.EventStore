extern alias eventstore;

using Hexalith.EventStore.IntegrationTests.Helpers;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Testing.Fakes;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using EventStoreProgram = eventstore::Program;

namespace Hexalith.EventStore.IntegrationTests.EventStore;

/// <summary>
/// WebApplicationFactory with JWT auth config and log capturing for auth failure tests.
/// </summary>
public class JwtLogCapturingFactory : WebApplicationFactory<EventStoreProgram> {
    public TestLogProvider LogProvider { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder) {
        ArgumentNullException.ThrowIfNull(builder);
        _ = builder.ConfigureAppConfiguration(config => config.AddInMemoryCollection(new Dictionary<string, string?> {
            ["Authentication:JwtBearer:Issuer"] = TestJwtTokenGenerator.Issuer,
            ["Authentication:JwtBearer:Audience"] = TestJwtTokenGenerator.Audience,
            ["Authentication:JwtBearer:SigningKey"] = TestJwtTokenGenerator.SigningKey,
            ["Authentication:JwtBearer:RequireHttpsMetadata"] = "false",
        }));
        _ = builder.ConfigureServices(services => {
            // Replace Dapr stores with InMemory for tests
            ServiceDescriptor? statusDescriptor = services.FirstOrDefault(
                d => d.ServiceType == typeof(ICommandStatusStore));
            if (statusDescriptor is not null) {
                _ = services.Remove(statusDescriptor);
            }

            _ = services.AddSingleton<ICommandStatusStore>(new InMemoryCommandStatusStore());

            ServiceDescriptor? archiveDescriptor = services.FirstOrDefault(
                d => d.ServiceType == typeof(ICommandArchiveStore));
            if (archiveDescriptor is not null) {
                _ = services.Remove(archiveDescriptor);
            }

            _ = services.AddSingleton<ICommandArchiveStore>(new InMemoryCommandArchiveStore());

            TestServiceOverrides.ReplaceCommandRouter(services);
            TestServiceOverrides.RemoveDaprHealthChecks(services);

            _ = services.AddLogging(logging => logging.AddProvider(LogProvider));
        });
    }
}
