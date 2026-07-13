using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.EventStore.DomainService;

/// <summary>
/// Provides explicit compatibility registration for legacy projection handlers.
/// </summary>
public static class DomainProjectionServiceCollectionExtensions {
    /// <summary>
    /// Adapts exactly one discovered legacy handler to exactly one named projection route.
    /// </summary>
    /// <typeparam name="THandler">The legacy handler implementation to adapt.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="domain">The exact canonical domain route.</param>
    /// <param name="projectionType">The exact canonical projection route.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddLegacyProjectionHandlerAdapter<THandler>(
        this IServiceCollection services,
        string domain,
        string projectionType)
        where THandler : class, IDomainProjectionHandler {
        ArgumentNullException.ThrowIfNull(services);

        _ = services.AddScoped<IAsyncDomainProjectionHandler>(provider => {
            THandler[] handlers = provider.GetServices<IDomainProjectionHandler>().OfType<THandler>().ToArray();
            if (handlers.Length != 1) {
                throw new InvalidOperationException(
                    $"Legacy projection adapter route '{domain}/{projectionType}' requires exactly one "
                    + $"registered '{typeof(THandler).FullName}' handler; found {handlers.Length}.");
            }

            return new LegacyDomainProjectionHandlerAdapter(handlers[0], domain, projectionType);
        });

        return services;
    }
}
