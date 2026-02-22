namespace Microsoft.Extensions.DependencyInjection;

using Hexalith.EventStore.Client.Handlers;

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
}
