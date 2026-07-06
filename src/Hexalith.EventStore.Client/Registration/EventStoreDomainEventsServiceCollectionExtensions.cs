using System.Reflection;

using Hexalith.EventStore.Client.Subscriptions;
using Hexalith.EventStore.Contracts.Events;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Client.Registration;

/// <summary>
/// Registers the generic domain-event consumer plumbing for services that subscribe to a domain's
/// published events via DAPR pub/sub.
/// </summary>
/// <remarks>
/// This is the platform generalization of the per-domain consumer registration domain modules previously
/// hand-wrote (e.g. <c>AddHexalithTenants</c> / <c>AddTenantEventHandler</c>). It registers the event-type
/// registry and the deduplicating <see cref="EventStoreDomainEventProcessor"/>; the consuming service
/// adds its own <see cref="IEventStoreDomainEventHandler{TEvent}"/> implementations.
/// </remarks>
public static class EventStoreDomainEventsServiceCollectionExtensions {
    private sealed class DomainEventHandlerRegistrationMarker<TEvent, THandler>
        where TEvent : IEventPayload
        where THandler : class, IEventStoreDomainEventHandler<TEvent>;

    /// <summary>
    /// Registers the domain-event consumer infrastructure: options, the deduplicating processor, and an
    /// event-type registry scanned from <paramref name="eventContractsAssembly"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="eventContractsAssembly">The assembly holding the domain's <see cref="IEventPayload"/> contracts.</param>
    /// <param name="configure">An optional delegate to configure <see cref="EventStoreDomainEventsOptions"/>.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddEventStoreDomainEvents(
        this IServiceCollection services,
        Assembly eventContractsAssembly,
        Action<EventStoreDomainEventsOptions>? configure = null) {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(eventContractsAssembly);

        OptionsBuilder<EventStoreDomainEventsOptions> optionsBuilder = services.AddOptions<EventStoreDomainEventsOptions>();
        if (configure is not null) {
            _ = optionsBuilder.Configure(configure);
        }

        IReadOnlyDictionary<string, Type> registry = BuildEventTypeRegistry(eventContractsAssembly);

        services.TryAddSingleton<IEventStoreDomainEventMarkerStore, InMemoryEventStoreDomainEventMarkerStore>();
        services.TryAddSingleton(sp => new EventStoreDomainEventProcessor(
            sp.GetRequiredService<IServiceScopeFactory>(),
            registry,
            sp.GetRequiredService<IEventStoreDomainEventMarkerStore>(),
            sp.GetRequiredService<ILogger<EventStoreDomainEventProcessor>>(),
            sp.GetRequiredService<IOptions<EventStoreDomainEventsOptions>>().Value.PayloadAggregateIdPropertyName));

        return services;
    }

    /// <summary>
    /// Replaces the default in-memory consumed-message marker store with the DAPR state-store backed marker store.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// Use this only when the consuming service's DAPR sidecar has access to the configured marker state
    /// store. The default registration stays in-memory so generic consumers do not implicitly require
    /// topology/state-store changes.
    /// </remarks>
    public static IServiceCollection AddDaprEventStoreDomainEventMarkerStore(this IServiceCollection services) {
        ArgumentNullException.ThrowIfNull(services);
        services.Replace(ServiceDescriptor.Singleton<IEventStoreDomainEventMarkerStore, DaprEventStoreDomainEventMarkerStore>());
        return services;
    }

    /// <summary>
    /// Registers a consumer handler for the specified event payload type.
    /// </summary>
    /// <typeparam name="TEvent">The event payload type to handle.</typeparam>
    /// <typeparam name="THandler">The handler implementation type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddEventStoreDomainEventHandler<TEvent, THandler>(this IServiceCollection services)
        where TEvent : IEventPayload
        where THandler : class, IEventStoreDomainEventHandler<TEvent> {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<THandler>();

        Type markerType = typeof(DomainEventHandlerRegistrationMarker<TEvent, THandler>);
        if (services.Any(s => s.ServiceType == markerType)) {
            return services;
        }

        _ = services.AddSingleton(markerType);
        _ = services.AddScoped<IEventStoreDomainEventHandler<TEvent>>(sp => sp.GetRequiredService<THandler>());

        return services;
    }

    private static IReadOnlyDictionary<string, Type> BuildEventTypeRegistry(Assembly assembly)
        => assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(IEventPayload).IsAssignableFrom(t))
            .ToDictionary(t => t.FullName!, t => t, StringComparer.Ordinal);
}
