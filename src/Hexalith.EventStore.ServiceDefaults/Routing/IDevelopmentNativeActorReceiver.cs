using Hexalith.EventStore.Contracts.Routing;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Hexalith.EventStore.ServiceDefaults.Routing;

/// <summary>SDK owner observation of actual native Dapr delegates and its registered actor implementation/method inventory.</summary>
internal interface IDevelopmentNativeActorReceiver
{
    /// <summary>Observes only an actual owner-installed native framework endpoint.</summary>
    RouteCatalogNativeActorTransport? Observe(RouteEndpoint endpoint, TechnicalHttpObservationPass? pass = null);
    /// <summary>Refuses unregistered methods/types and foreign caller identities, retaining native same-app channel trust.</summary>
    bool Allows(HttpContext context);
    /// <summary>Retains the existing typed projection-query policy inside the committed native scope.</summary>
    bool AllowsSnapshot(HttpContext context, CommittedRouteCatalogSnapshot snapshot);
}
