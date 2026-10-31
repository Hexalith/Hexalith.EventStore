using Hexalith.EventStore.Contracts.Authorization;
using Hexalith.EventStore.Contracts.Queries;

namespace Hexalith.EventStore.Contracts.Routing;

/// <summary>The existing SDK projection-query actor contract, independently bound to the gateway's actual registration.</summary>
public static class RouteCatalogProjectionActorPolicy
{
    /// <summary>The existing runtime actor type name.</summary>
    public const string ActorType = "ProjectionActor";
    /// <summary>The existing generic query method.</summary>
    public const string Method = nameof(IProjectionActor.QueryAsync);
    /// <summary>Only an explicit typed projection query may bind this existing SDK actor family.</summary>
    public static bool IsQuery(RouteContractDeclaration declaration)
    {
        ArgumentNullException.ThrowIfNull(declaration);
        return declaration.Kind == "query" && declaration.CredentialKind == "workload"
            && declaration.Execution is { TargetKind: "projection-actor", ActorType: ActorType, QueryKind: "projection" }
            && declaration.Operation == EventStoreOperationIdentifiers.ProjectionQuery;
    }
}
