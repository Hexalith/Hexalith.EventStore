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
        if (configuration["EventStore:RouteCatalog:DevelopmentBootstrap:Role"] == "aux-client")
        {
            if (configuration["EventStore:RouteCatalog:DevelopmentBootstrap:AuthorityRole"] is not null
                || !services.Any(descriptor => descriptor.ServiceType == typeof(DevelopmentAuxiliaryClientCatalogRegistration)))
                throw new RouteCatalogUnavailableException("The explicit auxiliary caller requires its installed strict receiver-free profile and cannot select a competing authority role.");
            return services;
        }
        if (configuration["EventStore:RouteCatalog:DevelopmentBootstrap:AuthorityRole"] != RouteCatalogAuthorityComponentPolicy.IsolatedRole)
            throw new RouteCatalogUnavailableException("Caller-only enrollment requires a dedicated, independently isolated authority component.");
        string actualAppId = configuration["EventStore:RouteCatalog:DevelopmentBootstrap:AppId"] ?? appId;
        return services.AddEventStoreDevelopmentRouteCatalogBootstrap(configuration, async (provider, token) =>
        {
            RouteCatalogReceiverSurface surface;
            if (TechnicalHttpRouteCatalogReceiver.IsEnabled(configuration))
            {
                var technical = provider.GetRequiredService<TechnicalHttpRouteCatalogReceiver>();
                await technical.WaitForCaptureAsync(token).ConfigureAwait(false);
                surface = technical.Observe();
            }
            else
            {
                var finite = provider.GetRequiredService<FiniteRouteCatalogReceiver>();
                await finite.WaitForCaptureAsync(token).ConfigureAwait(false);
                surface = finite.Observe();
            }
            var actual = await DevelopmentRouteCatalogInputs.CaptureAsync(configuration, actualAppId, [], [], [], [],
                DevelopmentTechnicalHttpTransport.Auxiliary(surface, actualAppId, "catalog-control"), token).ConfigureAwait(false);
            return actual with { Operations = [], ReceiverSurface = surface };
        });
    }

    /// <summary>Captures the real caller host using unchanged finite-v1 controls; ordinary UI routing and outbound credential channels stay intact.</summary>
    public static WebApplication UseEventStoreDevelopmentCallerCatalog(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        if (!string.IsNullOrEmpty(app.Configuration["EventStore:RouteCatalog:DevelopmentBootstrap:Directory"])
            && app.Configuration["EventStore:RouteCatalog:DevelopmentBootstrap:Role"] == "aux-client")
        {
            if (app.Services.GetService<DevelopmentAuxiliaryClientCatalogRegistration>() is null
                || app.Configuration["EventStore:RouteCatalog:DevelopmentBootstrap:AuthorityRole"] is not null)
                throw new RouteCatalogUnavailableException("The explicit auxiliary caller profile is not installed coherently.");
            return app;
        }
        return TechnicalHttpRouteCatalogReceiver.IsEnabled(app.Configuration)
            ? TechnicalHttpRouteCatalogReceiver.UseDevelopmentTechnicalHttpCatalogReceiver(app, ControlOperation)
            : FiniteRouteCatalogReceiver.UseFiniteDevelopmentRouteCatalogReceiver(app, ControlOperation);
    }

    internal static string? ControlOperation(RouteEndpoint endpoint)
    {
        if (endpoint.Metadata.GetMetadata<RouteCatalogControlEndpointMetadata>() is null) return null;
        string method = endpoint.RoutePattern.RawText?.Trim('/') ?? string.Empty;
        string? operation = method == RouteCatalogEndpointExtensions.ReadbackMethod ? EventStoreOperationIdentifiers.RouteCatalogRead
            : new[] { "prepare", "ready", "commit", "rollback", "reconcile" }.Any(stage => method == "internal/routing/catalog/" + stage)
                ? EventStoreOperationIdentifiers.RouteCatalogActivate : null;
        string policy = EventStoreWorkloadAuthenticationDefaults.OperationPolicy(RouteCatalogEndpointExtensions.WorkloadScheme, operation ?? string.Empty);
        return operation is not null && endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Select(data => data.Policy).SequenceEqual([policy], StringComparer.Ordinal)
            ? operation : null;
    }
}
