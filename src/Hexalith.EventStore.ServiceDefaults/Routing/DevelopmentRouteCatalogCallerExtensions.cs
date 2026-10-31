using Hexalith.EventStore.Contracts.Authorization;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.EventStore.ServiceDefaults.Routing;

/// <summary>Explicit enrollment for a selected caller that owns no domain handlers and receives only SDK catalog controls.</summary>
public static class DevelopmentRouteCatalogCallerExtensions
{
    /// <summary>Observes real auth, selected topology and actual endpoint metadata without inventing executable owner declarations.</summary>
    public static IServiceCollection AddEventStoreDevelopmentCallerCatalog(this IServiceCollection services, IConfiguration configuration, string appId)
    {
        ArgumentNullException.ThrowIfNull(services); ArgumentNullException.ThrowIfNull(configuration); ArgumentException.ThrowIfNullOrWhiteSpace(appId);
        if (string.IsNullOrEmpty(configuration["EventStore:RouteCatalog:DevelopmentBootstrap:Directory"])) return services;
        if (configuration["EventStore:RouteCatalog:DevelopmentBootstrap:AuthorityRole"] != RouteCatalogAuthorityComponentPolicy.IsolatedRole)
            throw new RouteCatalogUnavailableException("Caller-only enrollment requires a dedicated, independently isolated authority component.");
        string actualAppId = configuration["EventStore:RouteCatalog:DevelopmentBootstrap:AppId"] ?? appId;
        return services.AddEventStoreDevelopmentRouteCatalogBootstrap(configuration, async (provider, token) =>
        {
            var fence = provider.GetRequiredService<FiniteRouteCatalogReceiver>();
            await fence.WaitForCaptureAsync(token).ConfigureAwait(false);
            var surface = fence.Observe();
            var actual = await DevelopmentRouteCatalogInputs.CaptureAsync(configuration, actualAppId, [], [], [], [],
                surface.Endpoints.Where(endpoint => endpoint.Admitted).Select(endpoint => new RouteCatalogAuxiliaryOperation(actualAppId,
                    endpoint.Pattern.TrimStart('/'), endpoint.Operation!, "catalog-control")).ToArray(), token).ConfigureAwait(false);
            return actual with { Operations = [], ReceiverSurface = surface };
        });
    }

    /// <summary>Captures the real caller host using unchanged finite-v1 controls; ordinary UI routing and outbound credential channels stay intact.</summary>
    public static WebApplication UseEventStoreDevelopmentCallerCatalog(this WebApplication app)
        => FiniteRouteCatalogReceiver.UseFiniteDevelopmentRouteCatalogReceiver(app, endpoint =>
        {
            if (endpoint.Metadata.GetMetadata<RouteCatalogControlEndpointMetadata>() is null) return null;
            string method = endpoint.RoutePattern.RawText?.Trim('/') ?? string.Empty;
            string? operation = method == RouteCatalogEndpointExtensions.ReadbackMethod ? EventStoreOperationIdentifiers.RouteCatalogRead
                : new[] { "prepare", "ready", "commit", "rollback", "reconcile" }.Any(stage => method == "internal/routing/catalog/" + stage)
                    ? EventStoreOperationIdentifiers.RouteCatalogActivate : null;
            string policy = EventStoreWorkloadAuthenticationDefaults.OperationPolicy(RouteCatalogEndpointExtensions.WorkloadScheme, operation ?? string.Empty);
            return operation is not null && endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Select(data => data.Policy).SequenceEqual([policy], StringComparer.Ordinal)
                ? operation : null;
        });
}
