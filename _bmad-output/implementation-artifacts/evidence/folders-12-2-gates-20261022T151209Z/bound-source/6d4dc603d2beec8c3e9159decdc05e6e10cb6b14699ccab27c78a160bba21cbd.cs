using Dapr.Client;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Hexalith.EventStore.DomainService;

/// <summary>Registers the existing signed owner-query transport without enrolling any authority or credentials.</summary>
public static class DelegatedOwnerQueryServiceCollectionExtensions
{
    /// <summary>Adds the SDK transport and its dependencies; absent credentials remain unavailable at invocation.</summary>
    /// <param name="services">Host services; existing transport dependencies remain authoritative.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddEventStoreDelegatedOwnerQueries(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<IConfiguration>(new ConfigurationManager());
        if (!services.Any(static service => service.ServiceType == typeof(DaprClient))) services.AddDaprClient();
        services.AddHttpContextAccessor();
        services.AddEventStoreWorkloadAssertionIssuer();
        services.TryAddScoped<DelegatedOwnerQueryClient>();
        return services;
    }
}
