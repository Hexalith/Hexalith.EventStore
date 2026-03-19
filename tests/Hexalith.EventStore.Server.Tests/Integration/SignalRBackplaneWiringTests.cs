using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.StackExchangeRedis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Hexalith.EventStore.CommandApi.SignalR;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Integration;

public class SignalRBackplaneWiringTests {
    [Fact]
    public void AddEventStoreSignalR_WithRedisConnectionString_RegistersRedisHubLifetimeManager() {
        var configData = new Dictionary<string, string?> {
            ["EventStore:SignalR:Enabled"] = "true",
            ["EventStore:SignalR:BackplaneRedisConnectionString"] = "localhost:9999",
        };
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEventStoreSignalR(configuration);

        using ServiceProvider provider = services.BuildServiceProvider();
        HubLifetimeManager<ProjectionChangedHub> manager =
            provider.GetRequiredService<HubLifetimeManager<ProjectionChangedHub>>();
        manager.ShouldBeOfType<RedisHubLifetimeManager<ProjectionChangedHub>>();
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
        services.AddLogging();
        services.AddEventStoreSignalR(configuration);

        using ServiceProvider provider = services.BuildServiceProvider();
        HubLifetimeManager<ProjectionChangedHub> manager =
            provider.GetRequiredService<HubLifetimeManager<ProjectionChangedHub>>();
        manager.ShouldBeOfType<DefaultHubLifetimeManager<ProjectionChangedHub>>();
    }
}
