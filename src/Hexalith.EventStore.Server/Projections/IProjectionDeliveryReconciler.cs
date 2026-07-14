using Hexalith.EventStore.Contracts.Identity;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>Maintenance-only authoritative delivery receipt hydration.</summary>
internal interface IProjectionDeliveryReconciler {
    /// <summary>Hydrates exact receipts through the existing checkpoint without invoking handlers.</summary>
    Task<ProjectionDeliveryReconciliationResult> ReconcileFromEventStoreAsync(
        AggregateIdentity identity,
        string projectionName,
        string operatorId,
        CancellationToken cancellationToken = default);
}
