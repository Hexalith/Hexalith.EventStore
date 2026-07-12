using Dapr.Actors;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>
/// Lifecycle phase of a projection with respect to erasure. The
/// <see cref="ProjectionLifecycleActor"/> persists this phase so that projection delivery
/// writes and projection erasure are mutually exclusive per
/// (tenant, domain, aggregate, projection).
/// </summary>
public enum ProjectionLifecyclePhase {
    /// <summary>No erase is in progress; projection delivery writes are admitted.</summary>
    Idle = 0,

    /// <summary>An erase operation is in progress; projection delivery writes are deferred.</summary>
    Erasing = 1,
}

/// <summary>
/// Outcome kind of a <see cref="IProjectionLifecycleActor.BeginEraseAsync"/> admission decision.
/// </summary>
public enum ProjectionEraseAdmissionKind {
    /// <summary>The erase operation was newly admitted from the <see cref="ProjectionLifecyclePhase.Idle"/> phase.</summary>
    Admitted = 0,

    /// <summary>The same operation is already in progress; the caller resumes it.</summary>
    Resume = 1,

    /// <summary>A different operation is already in progress; admission is refused.</summary>
    Conflict = 2,
}

/// <summary>Request to begin (or resume) an erase operation.</summary>
/// <param name="OperationId">Stable identifier of the erase operation (idempotency/resume key).</param>
/// <param name="ManifestDigest">Digest of the erase target manifest, recorded on first admission.</param>
public sealed record ProjectionEraseBeginRequest(string OperationId, string ManifestDigest);

/// <summary>Admission decision returned by <see cref="IProjectionLifecycleActor.BeginEraseAsync"/>.</summary>
/// <param name="Kind">Whether the operation was admitted, resumed, or refused.</param>
/// <param name="PerTargetOutcomes">Per-target erase outcomes recorded so far (empty except on <see cref="ProjectionEraseAdmissionKind.Resume"/>).</param>
public sealed record ProjectionEraseAdmission(
    ProjectionEraseAdmissionKind Kind,
    IReadOnlyDictionary<string, string> PerTargetOutcomes);

/// <summary>Request to record the erase outcome for a single target.</summary>
/// <param name="OperationId">The in-flight erase operation identifier (must match the admitted operation).</param>
/// <param name="TargetKey">The target being erased.</param>
/// <param name="Outcome">The outcome recorded for the target.</param>
public sealed record ProjectionTargetOutcomeRequest(string OperationId, string TargetKey, string Outcome);

/// <summary>Request to complete an erase operation.</summary>
/// <param name="OperationId">The in-flight erase operation identifier (must match the admitted operation).</param>
public sealed record ProjectionEraseCompleteRequest(string OperationId);

/// <summary>Admission decision for a projection delivery write.</summary>
/// <param name="Admitted">True when the delivery write may proceed (phase is not <see cref="ProjectionLifecyclePhase.Erasing"/>).</param>
/// <param name="Phase">The lifecycle phase observed at admission time.</param>
public sealed record ProjectionDeliveryAdmission(bool Admitted, ProjectionLifecyclePhase Phase);

/// <summary>
/// DAPR actor that serializes projection delivery writes against projection erasure per
/// (tenant, domain, aggregate, projection). DAPR turn-based actor concurrency (one ActorId
/// per lifecycle scope) provides the cross-replica serialization primitive.
/// </summary>
public interface IProjectionLifecycleActor : IActor {
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
    /// <returns>An admission where <see cref="ProjectionDeliveryAdmission.Admitted"/> is false while erasing.</returns>
    Task<ProjectionDeliveryAdmission> TryAdmitDeliveryWriteAsync();
}
