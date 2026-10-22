using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Projections;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>Uses the existing dispatch carrier and owning SDK read-only per-event receipt operation.</summary>
internal interface IProjectionSourceGuardTransport
{
    /// <summary>Dispatches exactly one admitted event, preserving the original outer head dispatch identity.</summary>
    Task<ProjectionDispatchOutcome> DispatchEventAsync(AggregateIdentity identity, NamedProjectionRouteCatalogEntry catalog,
        string projection, ProjectionEventDto item, string dispatchId, CancellationToken cancellationToken);

    /// <summary>Reads the original existing terminal event batch without creating a new manifest.</summary>
    Task<ProjectionOwnerProofResult> ReadAppliedReceiptAsync(NamedProjectionRouteCatalogEntry catalog,
        ProjectionAppliedReceiptRequest request, CancellationToken cancellationToken);

    /// <summary>Resumes the persisted original batch only, after same-work reservation ownership is proven.</summary>
    Task<ProjectionOwnerProofResult> ReconcileAppliedReceiptAsync(NamedProjectionRouteCatalogEntry catalog,
        ProjectionAppliedReceiptRequest request, CancellationToken cancellationToken);

    /// <summary>Uses one existing staged-rebuild action over an immutable complete-prefix request.</summary>
    Task<ProjectionDispatchResponse?> InvokeReplayAsync(NamedProjectionRouteCatalogEntry catalog,
        ProjectionDispatchRequest request, ProjectionMappedReplayAction action, CancellationToken cancellationToken);

    /// <summary>Uploads the complete immutable mapped prefix through the existing bounded private part protocol.</summary>
    Task<ProjectionDispatchRequest?> PrepareReplayRequestAsync(NamedProjectionRouteCatalogEntry catalog,
        ProjectionDispatchRequest request, CancellationToken cancellationToken);

    /// <summary>Conditionally reclaims private replay preparation after the server proved delivery completion.</summary>
    Task<bool> ReclaimReplayAsync(NamedProjectionRouteCatalogEntry catalog,
        ProjectionDispatchRequest completed, CancellationToken cancellationToken);
}
