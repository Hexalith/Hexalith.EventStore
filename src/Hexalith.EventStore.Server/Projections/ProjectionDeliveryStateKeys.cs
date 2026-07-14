using Hexalith.EventStore.Contracts.Identity;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>Centralizes projection delivery state and control-plane key composition.</summary>
internal static class ProjectionDeliveryStateKeys {
    /// <summary>The store-global writer cutover marker key.</summary>
    public const string WriterProtocol = "projection-delivery-writer-protocol";

    private const string _reconciliationPrefix = "projection-delivery-reconciliation:";

    /// <summary>Returns the compatible in-place projection delivery row key.</summary>
    public static string GetStateKey(AggregateIdentity identity, string projectionName) =>
        ProjectionCheckpointTracker.GetProjectionScopedStateKey(identity, projectionName);

    /// <summary>Returns the payload-free reconciliation work key for one projection scope.</summary>
    public static string GetReconciliationKey(AggregateIdentity identity, string projectionName) {
        ArgumentNullException.ThrowIfNull(identity);
        ProjectionKeySegments.Validate(projectionName, nameof(projectionName));
        return $"{_reconciliationPrefix}{identity.ActorId}:{projectionName}";
    }
}
