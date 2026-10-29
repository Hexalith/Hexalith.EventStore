using Hexalith.EventStore.Contracts.Routing;

namespace Hexalith.EventStore.ServiceDefaults.Routing;

/// <summary>Checks projection methods and outputs without turning non-callable outputs into declarations.</summary>
public static class RouteCatalogProjectionPolicy
{
    /// <summary>Returns the exact allowed output inventory of a declared projection dispatch.</summary>
    public static IReadOnlyList<string> Outputs(CommittedRouteCatalogSnapshot snapshot, string tenant, string domain,
        string appId, string method, IReadOnlyList<string>? requested = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var declarations = snapshot.Envelope.Declarations.Where(d => d.Kind == "projection" && d.Domain == domain
            && snapshot.Envelope.Routes.Any(r => r.RouteId == d.RouteId && r.AppId == appId && r.Method == method
                && (r.ResolverTenantPattern is null or "*" || r.ResolverTenantPattern == tenant))).ToArray();
        if (declarations.Length == 0)
            throw new RouteCatalogUnavailableException("The projection method has no committed declaration.");
        if (declarations.Any(d => d.Execution is null))
            throw new RouteCatalogUnavailableException("The committed declaration has no execution metadata.");
        string[] outputs = [.. declarations.SelectMany(d => d.Execution!.ProjectionOutputs).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
        if (requested is not null && (requested.Count == 0 || requested.Distinct(StringComparer.Ordinal).Count() != requested.Count
            || requested.Any(type => !declarations.Any(d => d.ProjectionType == type))))
            throw new RouteCatalogUnavailableException("A projection output cannot select an undeclared callable handler.");
        return Array.AsReadOnly(outputs);
    }

    /// <summary>Checks an existing auxiliary method against its separate owner-bound inventory.</summary>
    public static void Auxiliary(CommittedRouteCatalogSnapshot snapshot, string domain, string appId, string method, string? operation = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (!snapshot.AuxiliaryOperations.Any(a => a.Domain == domain && a.AppId == appId && a.Method == method
            && (operation is null || a.Operation == operation)))
            throw new RouteCatalogUnavailableException("The auxiliary replay or reconstruction call is not inventoried for this target and domain.");
    }
}
