using Hexalith.EventStore.Contracts.Authorization;

namespace Hexalith.EventStore.Contracts.Routing;

/// <summary>Authentication contracts of the existing SDK human and closed workload HTTP endpoints.</summary>
public static class RouteCatalogSdkHttpPolicy
{
    /// <summary>Requires the existing human policy, or one exact existing workload action's route, verb and operation policy.</summary>
    public static bool Allows(string owner, string method, IReadOnlyList<string> verbs, IReadOnlyList<string> policies)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(method);
        ArgumentNullException.ThrowIfNull(verbs);
        ArgumentNullException.ThrowIfNull(policies);
        (string? operation, string verb) = (owner, method.Trim('/')) switch
        {
            ("Hexalith.EventStore.Controllers.CommandsController", "api/v1/commands/workload" or "api/v1/commands/bound/workload")
                => (EventStoreOperationIdentifiers.GatewayCommandSubmit, "POST"),
            ("Hexalith.EventStore.Controllers.CommandStatusController", "api/v1/commands/status/workload/{tenant}/{messageId}")
                => (EventStoreOperationIdentifiers.GatewayCommandStatus, "GET"),
            ("Hexalith.EventStore.Controllers.QueriesController", "api/v1/queries/owner-authority/workload")
                => (EventStoreOperationIdentifiers.GatewayOwnerAuthorityQuery, "POST"),
            ("Hexalith.EventStore.Controllers.StreamsController", "api/v1/streams/read/workload")
                => (EventStoreOperationIdentifiers.GatewayStreamRead, "POST"),
            ("Hexalith.EventStore.Controllers.TrustedEffectsController", "api/v1/trusted-effects")
                => (EventStoreOperationIdentifiers.TrustedEffect, "POST"),
            _ => (null, string.Empty),
        };
        return operation is null ? policies.Contains("EventStoreHuman", StringComparer.Ordinal)
            : verbs.SequenceEqual([verb], StringComparer.Ordinal) && policies.Contains("DaprInternal:" + operation, StringComparer.Ordinal);
    }
}
