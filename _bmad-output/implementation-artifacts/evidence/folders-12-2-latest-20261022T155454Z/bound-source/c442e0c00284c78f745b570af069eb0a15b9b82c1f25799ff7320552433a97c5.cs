using Hexalith.EventStore.SignalRHub;

using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.StackExchangeRedis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Integration;

public class SignalRBackplaneWiringTests {
    [Fact]
    public void AddEventStoreSignalR_WithRedisConnectionString_RegistersRedisHubLifetimeManager() {
        const string localRedisEndpoint = "localhost:9999";
        var configData = new Dictionary<string, string?> {
            ["EventStore:SignalR:Enabled"] = "true",
            ["EventStore:SignalR:BackplaneRedisConnectionString"] = localRedisEndpoint,
        };
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddEventStoreSignalR(configuration);

        using ServiceProvider provider = services.BuildServiceProvider();
        HubLifetimeManager<ProjectionChangedHub> manager =
            provider.GetRequiredService<HubLifetimeManager<ProjectionChangedHub>>();
        _ = manager.ShouldBeOfType<RedisHubLifetimeManager<ProjectionChangedHub>>();
    }

    [Fact]
    public void AddEventStoreSignalR_WithoutRedisConnectionString_UsesDefaultHubLifetimeManager() {
        var configData = new Dictionary<string, string?> {
            ["EventStore:SignalR:Enabled"] = "true",
        };
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddEventStoreSignalR(configuration);

        using ServiceProvider provider = services.BuildServiceProvider();
        HubLifetimeManager<ProjectionChangedHub> manager =
            provider.GetRequiredService<HubLifetimeManager<ProjectionChangedHub>>();
        _ = manager.ShouldBeOfType<DefaultHubLifetimeManager<ProjectionChangedHub>>();
    }
}
