using Hexalith.EventStore.Client.Projections;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Hexalith.EventStore.Client.Registration;

/// <summary>
/// Extension methods for registering the persisted read-model store.
/// </summary>
public static class ReadModelStoreServiceCollectionExtensions {
    /// <summary>
    /// Registers the DAPR-backed <see cref="IReadModelStore"/> for domain modules that maintain
    /// persisted, incrementally-updated read models. Requires a registered <c>DaprClient</c>
    /// (e.g. via <c>AddDaprClient</c>).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddEventStoreReadModelStore(this IServiceCollection services) {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<IReadModelStore, DaprReadModelStore>();
        return services;
    }
}
