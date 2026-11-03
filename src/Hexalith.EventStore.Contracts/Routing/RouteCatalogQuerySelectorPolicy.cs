namespace Hexalith.EventStore.Contracts.Routing;

/// <summary>Matches optional caller assertions only against the selected, validated committed query declaration.</summary>
public static class RouteCatalogQuerySelectorPolicy
{
    /// <summary>Preserves default selection when absent; supplied selectors must name the exact target or an owner-enrolled HTTP projection-query compatibility assertion.</summary>
    public static bool Matches(RouteContractDeclaration declaration, string? projectionActorType)
    {
        ArgumentNullException.ThrowIfNull(declaration);
        if (string.IsNullOrWhiteSpace(projectionActorType)) return true;
        return declaration.Kind == "query" && declaration.Execution is { } execution
            && (projectionActorType == execution.ActorType
                || execution is { TargetKind: "domain-service", QueryKind: "projection" }
                    && execution.CompatibilityProjectionActorTypes?.Contains(projectionActorType, StringComparer.Ordinal) == true);
    }
}
