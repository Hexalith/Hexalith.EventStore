using Dapr.Actors;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>
/// DAPR actor that serializes projection delivery writes against projection erasure per
/// (tenant, domain, aggregate, projection). DAPR turn-based actor concurrency (one ActorId
/// per lifecycle scope) provides the cross-replica serialization primitive.
/// </summary>
public interface IProjectionLifecycleActor : IActor {
    /// <summary>Begins or resumes the matching rebuild operation.</summary>
    /// <param name="request">The stable rebuild operation identity.</param>
    /// <returns>True when this operation owns the rebuilding phase.</returns>
    Task<bool> BeginRebuildAsync(ProjectionRebuildLifecycleRequest request);

    /// <summary>Completes the matching rebuild operation and returns to idle.</summary>
    /// <param name="request">The stable rebuild operation identity.</param>
    /// <returns>True when the matching operation was completed.</returns>
    Task<bool> CompleteRebuildAsync(ProjectionRebuildLifecycleRequest request);

    /// <summary>Fences promotion so terminal cleanup cannot race a matching rebuild write.</summary>
    /// <param name="request">The stable rebuild operation identity.</param>
    /// <returns>True when the matching rebuild owns the promotion fence.</returns>
    Task<bool> BeginRebuildPromotionAsync(ProjectionRebuildLifecycleRequest request);

    /// <summary>Releases the matching promotion fence while retaining the rebuilding phase.</summary>
    /// <param name="request">The stable rebuild operation identity.</param>
    /// <returns>True when the matching promotion fence was released.</returns>
    Task<bool> CompleteRebuildPromotionAsync(ProjectionRebuildLifecycleRequest request);

    /// <summary>Reads the persisted lifecycle phase.</summary>
    /// <returns>The current persisted phase, or idle when state is absent.</returns>
    Task<ProjectionLifecyclePhase> ReadPhaseAsync();

    /// <summary>Reads the phase together with its monotonic transition revision.</summary>
    /// <returns>Versioned persisted lifecycle evidence.</returns>
    Task<ProjectionLifecycleSnapshot> ReadSnapshotAsync();

    /// <summary>Begins or resumes a delivery lease that fences rebuild and erase admission.</summary>
    /// <param name="request">The stable delivery identity.</param>
    /// <returns>True when the matching delivery owns the lifecycle scope.</returns>
    Task<bool> BeginDeliveryWriteAsync(ProjectionDeliveryLifecycleRequest request);

    /// <summary>Releases the matching delivery lease after every projection write has finished.</summary>
    /// <param name="request">The stable delivery identity.</param>
    /// <returns>True when the matching lease was released.</returns>
    Task<bool> CompleteDeliveryWriteAsync(ProjectionDeliveryLifecycleRequest request);

    /// <summary>
    /// Begins (or resumes) an erase operation, transitioning the phase to
    /// <see cref="ProjectionLifecyclePhase.Erasing"/> when idle.
    /// </summary>
    /// <param name="request">The begin-erase request.</param>
    /// <returns>The admission decision.</returns>
    Task<ProjectionEraseAdmission> BeginEraseAsync(ProjectionEraseBeginRequest request);

    /// <summary>
    /// Records the erase outcome for a single target while the matching operation is in progress.
    /// </summary>
    /// <param name="request">The target-outcome request.</param>
    /// <returns>True when the outcome was recorded; false when no matching operation is in progress.</returns>
    Task<bool> RecordTargetOutcomeAsync(ProjectionTargetOutcomeRequest request);

    /// <summary>
    /// Completes the in-progress erase operation, returning the phase to
    /// <see cref="ProjectionLifecyclePhase.Idle"/>.
    /// </summary>
    /// <param name="request">The complete-erase request.</param>
    /// <returns>True when the operation was completed; false when no matching operation is in progress.</returns>
    Task<bool> CompleteEraseAsync(ProjectionEraseCompleteRequest request);

    /// <summary>
    /// Reports whether a projection delivery write is admitted at this instant.
    /// </summary>
    /// <returns>An admission where <see cref="ProjectionDeliveryAdmission.Admitted"/> is false while rebuilding or erasing.</returns>
    Task<ProjectionDeliveryAdmission> TryAdmitDeliveryWriteAsync();
}
