using System.Text.Json.Serialization;
using Dapr.Actors;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>
/// Persisted lifecycle phase of a projection with respect to rebuild and erasure. The
/// <see cref="ProjectionLifecycleActor"/> persists this phase so that projection delivery
/// writes and projection erasure are mutually exclusive per
/// (tenant, domain, aggregate, projection).
/// </summary>
public enum ProjectionLifecyclePhase {
    /// <summary>No rebuild or erase is in progress; projection delivery writes are admitted.</summary>
    Idle = 0,

    /// <summary>An erase operation is in progress; projection delivery writes are deferred.</summary>
    Erasing = 1,

    /// <summary>A rebuild operation is in progress; ordinary delivery writes are deferred.</summary>
    Rebuilding = 2,

    /// <summary>An admitted delivery owns the lifecycle scope until its projection write completes.</summary>
    Delivering = 3,
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

    /// <summary>
    /// The lifecycle scope is idle, but a fresh begin is not allowed by the caller's fail-closed rebuild gate.
    /// </summary>
    BeginNotAllowed = 3,
}

/// <summary>Request to begin (or resume) an erase operation.</summary>
/// <param name="OperationId">Stable identifier of the erase operation (idempotency/resume key).</param>
/// <param name="ManifestDigest">Digest of the erase target manifest, recorded on first admission.</param>
[method: JsonConstructor]
public sealed record ProjectionEraseBeginRequest(
    string OperationId,
    string ManifestDigest) {
    /// <summary>
    /// Initializes a begin request with an explicit fresh-begin permission.
    /// </summary>
    /// <param name="operationId">Stable identifier of the erase operation.</param>
    /// <param name="manifestDigest">Digest of the erase target manifest.</param>
    /// <param name="allowBegin">
    /// Whether an idle scope may transition to erasing. A matching in-flight operation may resume regardless.
    /// </param>
    public ProjectionEraseBeginRequest(string operationId, string manifestDigest, bool allowBegin)
        : this(operationId, manifestDigest) => AllowBegin = allowBegin;

    /// <summary>
    /// Gets a value indicating whether an idle scope may transition to erasing. The default preserves the
    /// released two-argument request behavior for callers compiled before the fresh-begin gate existed.
    /// </summary>
    public bool AllowBegin { get; init; } = true;
}

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
/// <param name="Admitted">True when the delivery write may proceed (phase is <see cref="ProjectionLifecyclePhase.Idle"/>).</param>
/// <param name="Phase">The lifecycle phase observed at admission time.</param>
public sealed record ProjectionDeliveryAdmission(bool Admitted, ProjectionLifecyclePhase Phase);

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
