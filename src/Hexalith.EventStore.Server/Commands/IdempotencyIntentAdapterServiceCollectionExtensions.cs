using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Hexalith.EventStore.Server.Commands;

/// <summary>Registers server-trusted idempotency intent adapters.</summary>
public static class IdempotencyIntentAdapterServiceCollectionExtensions
{
    /// <summary>Adds a trusted singleton adapter for one command type.</summary>
    /// <typeparam name="TAdapter">The adapter implementation type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddIdempotencyIntentAdapter<TAdapter>(this IServiceCollection services)
        where TAdapter : class, IIdempotencyIntentAdapter
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IIdempotencyIntentAdapter, TAdapter>());
        return services;
    }
}
