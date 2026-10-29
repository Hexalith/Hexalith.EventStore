using Dapr.Actors.Client;
using Dapr.Client;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Projections;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

/// <summary>Explicit test double for pre-existing actor/projection persistence protocols; catalog-enforced hosts use the separate owned catalog fixture.</summary>
internal static class LegacyProtocolCatalogIsolation
{
    public static T Create<T>(IServiceProvider services) where T : class
    {
        ArgumentNullException.ThrowIfNull(services);
        if (typeof(T) != typeof(NamedProjectionDispatchCoordinator))
            throw new InvalidOperationException("This protocol fixture isolates only its existing named coordinator.");
        // Call the explicit internal/friend constructor. Ordinary runtime activation must use
        // the public required-authority constructor; hiding a DI service cannot select isolation.
        return (T)(object)new NamedProjectionDispatchCoordinator(
            services.GetRequiredService<INamedProjectionRouteCatalog>(), services.GetRequiredService<IProjectionDeliveryCheckpointStore>(),
            services.GetRequiredService<IProjectionLifecycleGateway>(), services.GetRequiredService<IActorProxyFactory>(),
            services.GetRequiredService<DaprClient>(), services.GetRequiredService<IHttpClientFactory>(),
            services.GetRequiredService<IOptions<ProjectionDispatchOptions>>(), services.GetRequiredService<ILogger<NamedProjectionDispatchCoordinator>>(),
            retryScheduler: services.GetService<IProjectionDeliveryRetryScheduler>(), timeProvider: services.GetService<TimeProvider>(),
            catalogRefresher: services.GetService<INamedProjectionCatalogRefresher>(), idempotencyCoordinator: services.GetService<IProjectionDeliveryIdempotencyCoordinator>(),
            actorOptions: services.GetService<IOptions<EventStoreActorOptions>>(), historyReader: services.GetService<IProjectionDeliveryHistoryReader>(),
            catalogAuthority: null);
    }
}
