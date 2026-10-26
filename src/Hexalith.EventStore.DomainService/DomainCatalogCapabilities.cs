using System.Reflection;
using Hexalith.EventStore.Contracts.Routing;

namespace Hexalith.EventStore.DomainService;

/// <summary>Offline inspection of declarations attached to the actual compiled query handlers.</summary>
public static class DomainCatalogCapabilities
{
    /// <summary>Reads every concrete SDK query handler; undeclared handlers fail completeness rather than becoming actor fallbacks.</summary>
    public static IReadOnlyList<RouteContractDeclaration> Queries(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        Type[] types;
        try { types = assembly.GetTypes(); }
        catch (Exception exception) when (exception is ReflectionTypeLoadException or FileNotFoundException or FileLoadException)
        {
            throw new RouteCatalogValidationException([new("$.capabilities.queries", "unavailable-compiled-handler-metadata")]);
        }
        var routes = new List<RouteContractDeclaration>();
        foreach (Type type in types.Where(type => type is { IsClass: true, IsAbstract: false } && typeof(IDomainQueryHandler).IsAssignableFrom(type)))
        {
            if (type.GetProperty("CatalogDeclaration", BindingFlags.Public | BindingFlags.Static)?.GetValue(null) is not RouteContractDeclaration route
                || route.Kind != "query" || route.Execution?.TargetKind != "domain-service")
                throw new RouteCatalogValidationException([new("$.capabilities.queries", "missing-actual-handler-declaration")]);
            RouteCatalogValidator.ValidateDeclaration(route);
            routes.Add(route);
        }
        if (routes.Select(route => (route.Domain, route.MessageType)).Distinct().Count() != routes.Count)
            throw new RouteCatalogValidationException([new("$.capabilities.queries", "ambiguous-query-handler")]);
        return routes;
    }
}
