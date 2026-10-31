using Hexalith.EventStore.Contracts.Routing;
using Microsoft.AspNetCore.Routing;

namespace Hexalith.EventStore.ServiceDefaults.Routing;

/// <summary>SDK implementation boundary for an existing code-bound minimal HTTP receiver; only explicitly recognized SDK owners are admitted.</summary>
public interface IDevelopmentSdkHttpWitness
{
    /// <summary>Verifies the exact installed delegate, route, verb and unchanged human policy before describing its code owner.</summary>
    RouteCatalogHttpTransport? Observe(RouteEndpoint endpoint);
}
