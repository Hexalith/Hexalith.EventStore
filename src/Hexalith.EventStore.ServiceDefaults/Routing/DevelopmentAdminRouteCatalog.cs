using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.EventStore.ServiceDefaults.Routing;

/// <summary>Observes the existing authenticated SDK Admin API in one explicit private Development composition.</summary>
public static class DevelopmentAdminRouteCatalog
{
    /// <summary>Registers actual controller/code/ACL observations; normal and Production composition is unchanged.</summary>
    public static IServiceCollection AddEventStoreDevelopmentAdminCatalog(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        const string prefix = "EventStore:RouteCatalog:DevelopmentBootstrap:";
        if (string.IsNullOrEmpty(configuration[prefix + "Directory"])) return services;
        if (configuration[prefix + "Role"] != "admin-receiver")
            throw new InvalidOperationException("The SDK Admin receiver requires its explicit private admin-receiver role.");
        string appId = configuration["EventStore:DomainService:AppId"]
            ?? throw new InvalidOperationException("The owning runner must provide the actual Admin application identity.");
        return services.AddEventStoreDevelopmentRouteCatalogBootstrap(configuration, async (provider, token) =>
        {
            var fence = provider.GetRequiredService<FiniteRouteCatalogReceiver>();
            await fence.WaitForCaptureAsync(token).ConfigureAwait(false);
            var surface = fence.Observe();
            if (surface.FenceVersion != FiniteRouteCatalogReceiver.TechnicalVersion
                || !surface.Endpoints.Any(endpoint => endpoint.Http?.Owner.StartsWith("Hexalith.EventStore.Admin.Server.Controllers.", StringComparison.Ordinal) == true)
                || surface.Endpoints.Any(endpoint => endpoint.Http is { } http && !http.Owner.StartsWith("Hexalith.EventStore.Admin.Server.Controllers.", StringComparison.Ordinal)))
                throw new InvalidOperationException("The actual SDK Admin controller inventory is required.");
            if (configuration["DAPR_APP_ID"] is { Length: > 0 } sidecar && sidecar != appId)
                throw new InvalidOperationException("The actual Admin app identity differs from its sidecar.");
            var actual = await DevelopmentRouteCatalogInputs.CaptureAsync(configuration, appId, [], [], [], [],
                DevelopmentTechnicalHttpTransport.Auxiliary(surface, appId, "admin"), token).ConfigureAwait(false);
            return actual with { ReceiverSurface = surface };
        });
    }

    /// <summary>Installs the real finite technical receiver; actual JWT/admin policies and lifecycle authority still apply.</summary>
    public static WebApplication UseEventStoreDevelopmentAdminCatalog(this WebApplication app)
        => FiniteRouteCatalogReceiver.UseFiniteDevelopmentRouteCatalogReceiver(app, DevelopmentTechnicalHttpTransport.Operation, technical: true);
}
