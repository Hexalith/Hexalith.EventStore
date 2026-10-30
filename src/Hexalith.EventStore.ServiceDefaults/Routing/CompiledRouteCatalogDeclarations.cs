using System.Reflection;

using Hexalith.EventStore.Contracts.Routing;

namespace Hexalith.EventStore.ServiceDefaults.Routing;

/// <summary>Reads only explicit compiled owner sources and compares complete shared-codec declaration identities.</summary>
public static class CompiledRouteCatalogDeclarations
{
    /// <summary>Reads exact operation contracts beside Routes, rejecting missing or conflicting owner schemas and eligibility.</summary>
    public static IReadOnlyList<RouteOperationContract> ReadOperations(IEnumerable<Assembly> assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);
        var operations = new Dictionary<string, RouteOperationContract>(StringComparer.Ordinal);
        foreach (var source in assemblies.Distinct().SelectMany(a => a.GetCustomAttributes<RouteCatalogDeclarationSourceAttribute>()))
        {
            if (source.SourceType.GetProperty("Operations", BindingFlags.Public | BindingFlags.Static)?.GetValue(null)
                is not IEnumerable<RouteOperationContract> declared)
                throw new RouteCatalogUnavailableException($"Owner {source.SourceType.FullName} has no operation schemas or eligibility.");
            foreach (var operation in declared)
            {
                if (operation.Digest != RouteCatalogCodec.ComputeOperationContractDigest(operation)
                    || operations.ContainsKey(operation.RouteId))
                    throw new RouteCatalogUnavailableException($"Owner {source.SourceType.FullName} has conflicting operation {operation.RouteId}.");
                operations[operation.RouteId] = operation with { RequestSchema = operation.RequestSchema.Clone(), ResultSchema = operation.ResultSchema.Clone() };
            }
        }
        var routes = Read(assemblies);
        if (!routes.Select(r => r.RouteId).Order(StringComparer.Ordinal).SequenceEqual(operations.Keys.Order(StringComparer.Ordinal), StringComparer.Ordinal))
            throw new RouteCatalogUnavailableException("Compiled owner operation inventory differs from its routes.");
        return Array.AsReadOnly(operations.Values.OrderBy(o => o.RouteId, StringComparer.Ordinal).ToArray());
    }
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
                        _ = RouteCatalogCodec.ComputeDeclarationDigest(route);
                        if (declarations.ContainsKey(route.RouteId))
                            throw new RouteCatalogUnavailableException($"Duplicate or conflicting compiled owner declaration {route.RouteId}.");
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
