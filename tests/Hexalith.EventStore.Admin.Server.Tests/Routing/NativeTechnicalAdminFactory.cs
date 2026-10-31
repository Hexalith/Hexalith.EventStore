extern alias AdminHostRoot;
using Dapr.Client;
using Hexalith.EventStore.Admin.Abstractions.Services;
using Hexalith.EventStore.ServiceDefaults.Routing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Hexalith.EventStore.Admin.Server.Tests.Routing;

internal sealed class NativeTechnicalAdminFactory(IReadOnlyDictionary<string, string?> configuration, TechnicalCatalogMemoryStore store,
    DaprClient dapr, IDaprInfrastructureQueryService backend) : WebApplicationFactory<AdminHostRoot::Program>
{
    protected override IHost CreateHost(IHostBuilder builder)
    {
        // Deferred minimal hosting passes these host inputs before Program creates its builder,
        // so Add and Use observe the same actual private-run configuration.
        builder.ConfigureHostConfiguration(config => config.AddInMemoryCollection(configuration));
        return base.CreateHost(builder);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(configuration));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DaprClient>(); services.AddSingleton(dapr);
            services.RemoveAll<IRouteCatalogStateStore>(); services.AddSingleton<IRouteCatalogStateStore>(store);
            services.AddSingleton(backend);
        });
    }
}
