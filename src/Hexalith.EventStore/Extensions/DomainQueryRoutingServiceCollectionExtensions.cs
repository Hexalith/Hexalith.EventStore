using Hexalith.EventStore.Queries;
using Hexalith.EventStore.Server.Queries;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Hexalith.EventStore.Extensions;

/// <summary>
/// Registers capability-declared query routing: queries a domain advertises via <c>IDomainQueryHandler</c>
/// (per its operational-index metadata) are dispatched to the domain service's <c>/query</c> endpoint; all
/// other queries use the projection-actor router. Must be called <b>after</b> <c>AddEventStoreServer</c> so
/// the <see cref="HandlerAwareQueryRouter"/> decorator overrides the default <see cref="IQueryRouter"/>.
/// </summary>
public static class DomainQueryRoutingServiceCollectionExtensions {
    /// <summary>
    /// Adds the domain query-handler registry, the DAPR <c>/query</c> invoker, and the handler-aware
    /// <see cref="IQueryRouter"/> decorator.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddEventStoreDomainQueryRouting(this IServiceCollection services) {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IDomainQueryHandlerRegistry, DaprDomainQueryHandlerRegistry>();
        services.TryAddTransient<IDomainQueryInvoker, DaprDomainQueryInvoker>();

        // Register the concrete projection-actor router and decorate IQueryRouter with the handler-aware
        // router. The factory passes the concrete QueryRouter as the inner router (resolving IQueryRouter
        // here would resolve back to the decorator). Added after AddEventStoreServer's TryAddScoped, so the
        // last IQueryRouter registration — the decorator — wins.
        services.TryAddScoped<QueryRouter>();
        _ = services.AddScoped<IQueryRouter>(serviceProvider => new HandlerAwareQueryRouter(
            serviceProvider.GetRequiredService<QueryRouter>(),
            serviceProvider.GetRequiredService<IDomainQueryHandlerRegistry>(),
            serviceProvider.GetRequiredService<IDomainQueryInvoker>(),
            serviceProvider.GetRequiredService<ILogger<HandlerAwareQueryRouter>>()));

        return services;
    }
}
