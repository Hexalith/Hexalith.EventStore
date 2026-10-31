using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.Sample.Counter;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.AspNetCore.Routing;

namespace Hexalith.EventStore.Sample.Api.Services;

/// <summary>Enrolls only the actual generated Counter contracts for an explicit private Development run.</summary>
internal static class SampleApiCatalog
{
    /// <summary>Registers independently captured local inputs without inferring activation from a file.</summary>
    internal static IServiceCollection AddSampleDevelopmentCatalog(this IServiceCollection services, IConfiguration configuration)
    {
        if (string.IsNullOrEmpty(configuration["EventStore:RouteCatalog:DevelopmentBootstrap:Directory"])) return services;
        string appId = configuration["EventStore:DomainService:AppId"]
            ?? throw new RouteCatalogUnavailableException("The owning Development runner must supply the actual sample API app identity.");
        ArgumentException.ThrowIfNullOrWhiteSpace(appId);
        var owners = new[] { typeof(CounterCatalogDeclarations).Assembly };
        return services.AddEventStoreDevelopmentRouteCatalogBootstrap(configuration, async (provider, token) =>
        {
            var fence = provider.GetRequiredService<FiniteRouteCatalogReceiver>();
            await fence.WaitForCaptureAsync(token).ConfigureAwait(false);
            var surface = fence.Observe();
            var actual = await DevelopmentRouteCatalogInputs.CaptureAsync(configuration, appId,
                CompiledRouteCatalogDeclarations.Read(owners), [], [], [],
                surface.Endpoints.Where(endpoint => endpoint.Admitted)
                    .Select(endpoint => new RouteCatalogAuxiliaryOperation(appId, endpoint.Pattern.TrimStart('/'), endpoint.Operation!, "counter")).ToArray(), token).ConfigureAwait(false);
            return actual with { ReceiverSurface = surface, Operations = CompiledRouteCatalogDeclarations.ReadOperations(owners) };
        });
    }

    /// <summary>Reads operation identity from the actual generated endpoint's compiled owner contract.</summary>
    internal static string? Operation(RouteEndpoint endpoint)
    {
        var generated = endpoint.Metadata.GetMetadata<RouteCatalogEndpointAttribute>();
        if (generated is null) return null;
        return CounterCatalogDeclarations.Routes.Single(route => route.Kind == generated.Kind
            && route.Execution!.HandlerType == generated.ContractType.FullName).Operation;
    }
}
