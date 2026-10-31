using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

[assembly: HostingStartup(typeof(Hexalith.EventStore.ServiceDefaults.Routing.DevelopmentAuxiliaryClientCatalogHostingStartup))]

namespace Hexalith.EventStore.ServiceDefaults.Routing;

/// <summary>Opt-in hosting startup for the private Development runner's receiver-free catalog participants.</summary>
public sealed class DevelopmentAuxiliaryClientCatalogHostingStartup : IHostingStartup
{
    /// <inheritdoc/>
    public void Configure(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ConfigureServices((context, services) =>
        {
            const string prefix = "EventStore:RouteCatalog:DevelopmentBootstrap:";
            string? role = context.Configuration[prefix + "Role"];
            if (role is null) return;
            if (role != "aux-client" || !context.HostingEnvironment.IsDevelopment()
                || string.IsNullOrEmpty(context.Configuration[prefix + "Directory"]))
                throw new InvalidOperationException("Auxiliary catalog startup requires its explicit private Development aux-client profile.");
            if (services.Any(descriptor => descriptor.ServiceType == typeof(DevelopmentAuxiliaryClientCatalogRegistration))) return;
            services.AddSingleton<DevelopmentAuxiliaryClientCatalogRegistration>();
            services.AddEventStoreDevelopmentRouteCatalogBootstrap(context.Configuration,
                (provider, token) => DevelopmentAuxiliaryClientCatalog.CaptureAsync(provider, context.Configuration, token));
        });
    }
}
