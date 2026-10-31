using System.Reflection;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.EventStore.ServiceDefaults.Routing;

/// <summary>Explicitly enrolls generated public human GET hosts in the owning Development composition.</summary>
public static class GeneratedRestRouteCatalogExtensions
{
    /// <summary>Captures actual generated endpoint metadata and complete compiled owner schemas for activation.</summary>
    public static IServiceCollection AddEventStoreDevelopmentGeneratedRestCatalog(this IServiceCollection services,
        IConfiguration configuration, string appId, params Assembly[] contractOwners)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(appId);
        ArgumentNullException.ThrowIfNull(contractOwners);
        if (string.IsNullOrEmpty(configuration["EventStore:RouteCatalog:DevelopmentBootstrap:Directory"])) return services;
        string actualAppId = configuration["EventStore:RouteCatalog:DevelopmentBootstrap:AppId"] ?? appId;
        services.AddSingleton<GeneratedRestRouteCatalogReceiver>();
        return services.AddEventStoreDevelopmentRouteCatalogBootstrap(configuration, async (provider, token) =>
        {
            var receiver = provider.GetRequiredService<GeneratedRestRouteCatalogReceiver>();
            await receiver.WaitForCaptureAsync(token).ConfigureAwait(false);
            var actual = await DevelopmentRouteCatalogInputs.CaptureAsync(configuration, actualAppId,
                CompiledRouteCatalogDeclarations.Read(contractOwners), [], [], [], [], token).ConfigureAwait(false);
            return actual with { ReceiverSurface = receiver.Observe(), Operations = CompiledRouteCatalogDeclarations.ReadOperations(contractOwners) };
        });
    }

    /// <summary>Fences app-channel deliveries before generated actions while preserving ordinary human HTTP.</summary>
    public static WebApplication UseEventStoreDevelopmentGeneratedRestCatalogReceiver(this WebApplication app)
        => GeneratedRestRouteCatalogReceiver.UseDevelopmentGeneratedRestCatalogReceiver(app);
}
