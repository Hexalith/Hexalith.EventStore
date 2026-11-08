using System.Reflection;

using Hexalith.EventStore.DomainService;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Hexalith.EventStore.Server.Extensions;

/// <summary>Loads composer-declared extension packages into a composed gateway.</summary>
public static class EventStoreExtensionServiceCollectionExtensions
{
    /// <summary>The configuration section holding the declared packages, one indexed entry per package.</summary>
    /// <remarks>Each entry supplies <c>PackageId</c>, <c>ExtensionId</c> and <c>ApiMajor</c>.</remarks>
    public const string ConfigurationSection = "EventStore:Extensions:Packages";

    /// <summary>The readiness check that lists the loaded extension identities and majors.</summary>
    public const string HealthCheckName = "eventstore-extensions";

    /// <summary>
    /// Loads every declared extension assembly, verifies its identity attribute against its declaration, refuses
    /// unsupported or duplicate extensions, and registers its adapters through the trusted adapter registry.
    /// </summary>
    /// <remarks>Register extensions before AddEventStoreServer or AddEventStoreRunScopedCatalogGateway so the catalog
    /// bootstrap reuses their adapters. With no declared extension the services are unchanged.</remarks>
    /// <param name="services">The gateway services.</param>
    /// <param name="configuration">The gateway configuration carrying <see cref="ConfigurationSection"/>.</param>
    /// <returns>The services for chaining.</returns>
    /// <exception cref="EventStoreExtensionException">A declaration or loaded package is refused; the host must not start.</exception>
    public static IServiceCollection AddEventStoreExtensions(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        IReadOnlyList<EventStoreExtensionDeclaration> declarations = EventStoreExtensionLoader.Read(configuration);
        return declarations.Count == 0
            ? services
            : services.AddEventStoreExtensions(declarations, static name => Assembly.Load(new AssemblyName(name)));
    }

    /// <summary>Loads and registers explicit declarations with a supplied assembly loader.</summary>
    /// <param name="services">The gateway services.</param>
    /// <param name="declarations">The composer declarations.</param>
    /// <param name="load">Loads one package assembly by name.</param>
    /// <returns>The services for chaining.</returns>
    internal static IServiceCollection AddEventStoreExtensions(
        this IServiceCollection services,
        IReadOnlyList<EventStoreExtensionDeclaration> declarations,
        Func<string, Assembly> load)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (services.Any(descriptor => descriptor.ServiceType == typeof(EventStoreExtensionCatalog)))
        {
            throw new EventStoreExtensionException(
                EventStoreExtensionConditions.DuplicateRegistration,
                "EventStore extensions are already registered on this gateway; compose them once.");
        }

        EventStoreExtensionCatalog catalog = EventStoreExtensionLoader.Load(declarations, load);
        foreach (IDeclaredIdempotencyIntentAdapter adapter in catalog.Adapters)
        {
            // The shared registry rejects duplicate or overlapping adapters when the host starts.
            _ = services.AddSingleton<IIdempotencyIntentAdapter>(adapter);
        }

        _ = services.AddSingleton(catalog);
        _ = services.AddHealthChecks().AddCheck<EventStoreExtensionsHealthCheck>(HealthCheckName, HealthStatus.Unhealthy, ["ready"]);
        return services;
    }
}
