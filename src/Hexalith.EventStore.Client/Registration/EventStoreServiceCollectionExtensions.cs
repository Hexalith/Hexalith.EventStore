
using System.Reflection;
using System.Runtime.CompilerServices;

using Hexalith.EventStore.Client.Configuration;
using Hexalith.EventStore.Client.Discovery;
using Hexalith.EventStore.Client.Handlers;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.EventStore.Client.Registration;

/// <summary>
/// Extension methods for registering Event Store client services in the dependency injection container.
/// </summary>
public static class EventStoreServiceCollectionExtensions {
    /// <summary>
    /// Registers a domain processor implementation in the dependency injection container with scoped lifetime.
    /// </summary>
    /// <typeparam name="TProcessor">The domain processor implementation type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddEventStoreClient<TProcessor>(this IServiceCollection services)
        where TProcessor : class, IDomainProcessor {
        ArgumentNullException.ThrowIfNull(services);
        return services.AddScoped<IDomainProcessor, TProcessor>();
    }

    /// <summary>
    /// Auto-discovers aggregate and projection types in the calling assembly,
    /// registers discovered aggregates in the DI container, and stores full discovery results.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static IServiceCollection AddEventStore(this IServiceCollection services) {
        ArgumentNullException.ThrowIfNull(services);
        return AddEventStoreCore(services, configureOptions: null, [Assembly.GetCallingAssembly()]);
    }

    /// <summary>
    /// Auto-discovers aggregate and projection types in the calling assembly,
    /// registers discovered aggregates with the specified global options, and stores full discovery results.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">A delegate to configure <see cref="EventStoreOptions"/>.</param>
    /// <returns>The service collection for chaining.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static IServiceCollection AddEventStore(this IServiceCollection services, Action<EventStoreOptions> configureOptions) {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureOptions);
        return AddEventStoreCore(services, configureOptions, [Assembly.GetCallingAssembly()]);
    }

    /// <summary>
    /// Scans the specified assemblies for aggregate and projection types and registers them in the DI container.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="assemblies">The assemblies to scan for domain types.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddEventStore(this IServiceCollection services, params Assembly[] assemblies) {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(assemblies);
        if (assemblies.Length == 0) {
            throw new ArgumentException("At least one assembly must be specified.", nameof(assemblies));
        }

        return AddEventStoreCore(services, configureOptions: null, assemblies);
    }

    /// <summary>
    /// Scans the specified assemblies for aggregate and projection types and registers them with the specified global options.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">A delegate to configure <see cref="EventStoreOptions"/>.</param>
    /// <param name="assemblies">The assemblies to scan for domain types.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddEventStore(this IServiceCollection services, Action<EventStoreOptions> configureOptions, params Assembly[] assemblies) {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureOptions);
        ArgumentNullException.ThrowIfNull(assemblies);
        if (assemblies.Length == 0) {
            throw new ArgumentException("At least one assembly must be specified.", nameof(assemblies));
        }

        return AddEventStoreCore(services, configureOptions, assemblies);
    }

    private static IServiceCollection AddEventStoreCore(
        IServiceCollection services,
        Action<EventStoreOptions>? configureOptions,
        IEnumerable<Assembly> assemblies) {
        // Idempotency: skip if already registered
        if (services.Any(s => s.ServiceType == typeof(DiscoveryResult))) {
            return services;
        }

        // Configure options if provided
        if (configureOptions is not null) {
            _ = services.Configure(configureOptions);
        }

        // Opportunistic appsettings binding (AC3): bind EventStore section when IConfiguration is available.
        IConfiguration? configuration = TryGetConfiguration(services);
        if (configuration is not null) {
            _ = services.Configure<EventStoreOptions>(configuration.GetSection("EventStore"));
        }

        // Scan assemblies for domain types
        DiscoveryResult discoveryResult = AssemblyScanner.ScanForDomainTypes(assemblies);

        // Register DiscoveryResult as singleton
        _ = services.AddSingleton(discoveryResult);

        // Register empty activation context (populated by UseEventStore())
        _ = services.AddSingleton<EventStoreActivationContext>();

        // Register each discovered aggregate as IDomainProcessor
        foreach (DiscoveredDomain aggregate in discoveryResult.Aggregates) {
            // Non-keyed: backward compat + enumeration of all processors
            _ = services.AddScoped(typeof(IDomainProcessor), aggregate.Type);

            // Keyed: domain-specific resolution (forward-looking for actor pipeline)
            _ = services.AddKeyedScoped(typeof(IDomainProcessor), aggregate.DomainName, aggregate.Type);
        }

        return services;
    }

    private static IConfiguration? TryGetConfiguration(IServiceCollection services) {
        ArgumentNullException.ThrowIfNull(services);

        ServiceDescriptor? descriptor = services.LastOrDefault(static s => s.ServiceType == typeof(IConfiguration));
        if (descriptor?.ImplementationInstance is IConfiguration configurationInstance) {
            return configurationInstance;
        }

        if (descriptor is null) {
            return null;
        }

        using ServiceProvider tempProvider = services.BuildServiceProvider();
        return tempProvider.GetService<IConfiguration>();
    }
}
