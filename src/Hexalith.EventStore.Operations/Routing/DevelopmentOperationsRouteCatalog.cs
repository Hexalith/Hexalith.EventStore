using Hexalith.EventStore.ServiceDefaults.Routing;

namespace Hexalith.EventStore.Operations.Routing;

/// <summary>Enables actual SDK Operations receiver observations in the owning private Development cohort.</summary>
internal static class DevelopmentOperationsRouteCatalog
{
    internal static IServiceCollection AddDevelopmentOperationsCatalog(this IServiceCollection services, IConfiguration configuration)
    {
        const string prefix = "EventStore:RouteCatalog:DevelopmentBootstrap:";
        if (string.IsNullOrEmpty(configuration[prefix + "Directory"])) return services;
        if (configuration[prefix + "Role"] != "operations-receiver")
            throw new InvalidOperationException("Operations requires its explicit private receiver role.");
        string appId = configuration["EventStore:DomainService:AppId"]
            ?? throw new InvalidOperationException("The owning runner must provide the actual Operations identity.");
        return services.AddEventStoreDevelopmentRouteCatalogBootstrap(configuration, async (provider, token) =>
        {
            var fence = provider.GetRequiredService<FiniteRouteCatalogReceiver>();
            await fence.WaitForCaptureAsync(token).ConfigureAwait(false);
            var surface = fence.Observe();
            string owner = typeof(Endpoints.DeadLetterOperationsEndpointExtensions).FullName!;
            if (surface.FenceVersion != FiniteRouteCatalogReceiver.TechnicalVersion
                || surface.Endpoints.Count(endpoint => endpoint.Http?.Owner == owner) != 5
                || surface.Endpoints.Any(endpoint => endpoint.Http is { } http && http.Owner != owner))
                throw new InvalidOperationException("The complete actual SDK Operations endpoint inventory is required.");
            if (configuration["DAPR_APP_ID"] is { Length: > 0 } sidecar && sidecar != appId)
                throw new InvalidOperationException("The actual Operations identity differs from its sidecar.");
            var actual = await DevelopmentRouteCatalogInputs.CaptureAsync(configuration, appId, [], [], [], [],
                DevelopmentTechnicalHttpTransport.Auxiliary(surface, appId, "eventstore-operations"), token).ConfigureAwait(false);
            return actual with { ReceiverSurface = surface };
        });
    }

    internal static WebApplication UseDevelopmentOperationsCatalog(this WebApplication app)
        => FiniteRouteCatalogReceiver.UseFiniteDevelopmentRouteCatalogReceiver(app, DevelopmentTechnicalHttpTransport.Operation, technical: true);
}
