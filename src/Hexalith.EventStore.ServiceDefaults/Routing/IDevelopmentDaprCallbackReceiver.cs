using Hexalith.EventStore.Contracts.Routing;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Hexalith.EventStore.ServiceDefaults.Routing;

/// <summary>SDK owner observation of its actual Dapr discovery and signed projection notification callbacks.</summary>
internal interface IDevelopmentDaprCallbackReceiver
{
    /// <summary>Observes only the installed SDK mapper/action with its exact topic and channel metadata.</summary>
    RouteCatalogDaprCallbackTransport? Observe(RouteEndpoint endpoint, TechnicalHttpObservationPass? pass = null);
    /// <summary>Recognizes the actual absent programmatic discovery protocol in explicitly declarative mode.</summary>
    bool IsAbsentDiscovery(HttpContext context);
}
