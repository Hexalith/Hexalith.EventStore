using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.ServiceDefaults.Routing;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Substituted observer whose methods must never be reached by technical enrollment.</summary>
internal sealed class SubstitutedNativeCatalogObserver : IDevelopmentNativeActorReceiver
{
    public RouteCatalogNativeActorTransport? Observe(RouteEndpoint endpoint, TechnicalHttpObservationPass? pass = null) => throw new InvalidOperationException("Substituted observer was invoked.");
    public bool Allows(HttpContext context) => throw new InvalidOperationException("Substituted observer was invoked.");
    public bool AllowsSnapshot(HttpContext context, CommittedRouteCatalogSnapshot snapshot) => throw new InvalidOperationException("Substituted observer was invoked.");
}
