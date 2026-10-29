using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Hexalith.EventStore.ServiceDefaults.Authentication;

namespace Hexalith.EventStore.ServiceDefaults.Routing;

/// <summary>Common fail-closed runtime catalog wiring for gateway, domain and generated hosts.</summary>
public static class RouteCatalogServiceCollectionExtensions
{
    /// <summary>Registers the composition owner's independent observation of actual loaded host inputs.</summary>
    public static IServiceCollection AddEventStoreRouteCatalogObservation(this IServiceCollection services,
        Func<IServiceProvider, CancellationToken, Task<RouteCatalogHostObservation>> observe)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(observe);
        services.AddSingleton<IRouteCatalogHostObserver>(provider => new ObservedRouteCatalogHost(async token =>
        {
            var actual = await observe(provider, token).ConfigureAwait(false);
            var coordinates = provider.GetRequiredService<IRouteCatalogStateStore>().Coordinates
                ?? throw new RouteCatalogUnavailableException("The authority state store has no independently selected coordinates.", RouteCatalogRefusalReason.Configuration);
            return actual with { AuthorityCoordinates = coordinates };
        }));
        return services;
    }

    /// <summary>Registers shared Dapr activation without inferring activation from declarations or configuration.</summary>
    public static IServiceCollection AddEventStoreRouteCatalog(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        if (services.Any(d => d.ServiceType == typeof(RouteCatalogRegistration))) return services;
        services.AddSingleton<RouteCatalogRegistration>();
        services.TryAddSingleton<IConfiguration>(configuration);
        services.AddOptions<RouteCatalogHostOptions>().Bind(configuration.GetSection("EventStore:RouteCatalog"));
        services.AddHttpClient("route-catalog-state");
        services.TryAddEnumerable(ServiceDescriptor.Singleton<Microsoft.Extensions.Http.IHttpMessageHandlerBuilderFilter, RouteCatalogStateHttpClientFilter>());
        services.TryAddSingleton<IRouteCatalogStateStore, DaprRouteCatalogStateStore>();
        services.TryAddSingleton<IRouteCatalogTrustValidator, DevelopmentRouteCatalogTrustValidator>();
        services.TryAddSingleton<IRouteCatalogAuthority, RouteCatalogAuthority>();
        services.TryAddScoped<GeneratedRouteCatalogFilter>();
        services.Configure<Microsoft.AspNetCore.Mvc.MvcOptions>(mvc => mvc.Filters.AddService<GeneratedRouteCatalogFilter>());
        services.AddAuthentication()
            .AddEventStoreSidecarChannelScheme()
            .AddEventStoreWorkloadScheme(RouteCatalogEndpointExtensions.WorkloadScheme, "Authentication:RouteCatalogWorkload");
        services.AddEventStoreWorkloadPolicies(RouteCatalogEndpointExtensions.WorkloadScheme,
            [Hexalith.EventStore.Contracts.Authorization.EventStoreOperationIdentifiers.RouteCatalogRead,
             Hexalith.EventStore.Contracts.Authorization.EventStoreOperationIdentifiers.RouteCatalogActivate]);
        services.AddHealthChecks().AddCheck<RouteCatalogReadinessCheck>("route-catalog", tags: ["ready"]);
        return services;
    }
}
