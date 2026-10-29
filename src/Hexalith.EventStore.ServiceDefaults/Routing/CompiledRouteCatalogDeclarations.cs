using System.Reflection;

using Hexalith.EventStore.Contracts.Routing;

namespace Hexalith.EventStore.ServiceDefaults.Routing;

/// <summary>Reads only explicit compiled owner sources and compares complete shared-codec declaration identities.</summary>
public static class CompiledRouteCatalogDeclarations
{
    /// <summary>Returns immutable declarations; absent, inaccessible or conflicting owner sources refuse certification.</summary>
    public static IReadOnlyList<RouteContractDeclaration> Read(IEnumerable<Assembly> assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);
        var declarations = new Dictionary<string, RouteContractDeclaration>(StringComparer.Ordinal);
        try
        {
            foreach (Assembly assembly in assemblies.Distinct())
            {
                foreach (RouteCatalogDeclarationSourceAttribute source in assembly.GetCustomAttributes<RouteCatalogDeclarationSourceAttribute>())
                {
                    if (source.SourceType.GetProperty("Routes", BindingFlags.Public | BindingFlags.Static)?.GetValue(null)
                        is not IEnumerable<RouteContractDeclaration> routes)
                        throw new RouteCatalogUnavailableException("The compiled owner declaration source is unavailable.");
                    foreach (RouteContractDeclaration route in routes)
                    {
                        string digest = RouteCatalogCodec.ComputeDeclarationDigest(route);
                        if (declarations.TryGetValue(route.RouteId, out var previous) && RouteCatalogCodec.ComputeDeclarationDigest(previous) != digest)
                            throw new RouteCatalogUnavailableException("Conflicting compiled owner declarations.");
                        declarations[route.RouteId] = route with
                        {
                            FallbackTypes = Array.AsReadOnly(route.FallbackTypes.ToArray()),
                            Execution = route.Execution is { } e ? e with
                            {
                                Aliases = Array.AsReadOnly(e.Aliases.ToArray()), ProjectionOutputs = Array.AsReadOnly(e.ProjectionOutputs.ToArray()),
                            } : null,
                        };
                    }
                }
            }
        }
        catch (Exception exception) when (exception is not RouteCatalogUnavailableException and not RouteCatalogValidationException)
        {
            throw new RouteCatalogUnavailableException("The complete compiled declaration inventory cannot be read.");
        }
        if (declarations.Count == 0) throw new RouteCatalogUnavailableException("No compiled owner declaration source was enrolled.");
        return Array.AsReadOnly(declarations.Values.OrderBy(d => d.RouteId, StringComparer.Ordinal).ToArray());
    }
}
