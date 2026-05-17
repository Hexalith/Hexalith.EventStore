using Hexalith.EventStore.Contracts.Streams;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>
/// Persists projection rebuild checkpoints with optimistic concurrency and monotonic progress.
/// </summary>
public interface IProjectionRebuildCheckpointStore {
    /// <summary>
    /// Reads a projection rebuild checkpoint.
    /// </summary>
    Task<ProjectionRebuildCheckpoint?> ReadAsync(
        ProjectionRebuildCheckpointScope scope,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves projection rebuild progress without lowering existing progress.
    /// </summary>
    /// <param name="isPerAggregateProgress">
    /// When true, the caller is recording per-aggregate progress for a domain-wide rebuild and
    /// the per-aggregate scope's OperationId is inherited from the operator-scope row. The
    /// implementation MUST skip the OperationId-mismatch guard (<c>operation-in-flight</c>) for
    /// per-aggregate progress writes because the operator-scope row is the single source of
    /// operator identity; per-aggregate rows are progress-only and may carry a prior operator's
    /// OperationId after a Reset+Replay sequence (pass-5 P-DEC6-5P / pass-4 DEC7-4P).
    /// </param>
    Task<ProjectionRebuildCheckpointSaveResult> SaveAsync(
        ProjectionRebuildCheckpointScope scope,
        long lastAppliedSequence,
        ProjectionRebuildStatus status,
        string? failureReasonCode = null,
        CancellationToken cancellationToken = default,
        long? toPosition = null,
        bool isPerAggregateProgress = false);

    /// <summary>
    /// Explicitly rewinds projection rebuild progress with optimistic concurrency.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method intentionally bypasses the monotonic progress and lifecycle protections used by
    /// <see cref="SaveAsync"/>. Callers must enforce operator authorization and only use it for
    /// explicit lifecycle commands such as reset, replay, or retry.
    /// </para>
    /// <para>
    /// Terminal-record audit-trail policy (per pass-4 DEC4): when an existing record is in a
    /// terminal status (<see cref="ProjectionRebuildStatus.Succeeded"/> /
    /// <see cref="ProjectionRebuildStatus.Failed"/> / <see cref="ProjectionRebuildStatus.Canceled"/>),
    /// <see cref="ResetAsync"/> overwrites it with the caller's <see cref="ProjectionRebuildCheckpointScope.OperationId"/>
    /// even when that value differs from the persisted <c>OperationId</c>. Operator-intentional
    /// rewinds (Replay/Reset/Retry) are designed to replace prior operator history; per-operation
    /// audit trail loss across consecutive operators is accepted by design. Use the operation-id
    /// trail captured by structured logs and OpenTelemetry activities (not the persisted scope row)
    /// when reconstructing per-operator history.
    /// </para>
    /// </remarks>
    Task<ProjectionRebuildCheckpointSaveResult> ResetAsync(
        ProjectionRebuildCheckpointScope scope,
        long lastAppliedSequence,
        ProjectionRebuildStatus status,
        string? failureReasonCode = null,
        CancellationToken cancellationToken = default,
        long? toPosition = null);

    /// <summary>
    /// Returns true when any projection in the (tenant, domain) pair has an active operator rebuild.
    /// </summary>
    /// <remarks>
    /// D3-B: backed by a per-(tenant, domain) active-rebuild index that is maintained by
    /// <see cref="SaveAsync"/> and <see cref="ResetAsync"/>. The poller calls this method to
    /// avoid racing an in-flight operator rebuild without assuming projectionName == domain.
    /// </remarks>
    Task<bool> HasActiveOperatorRebuildForDomainAsync(
        string tenant,
        string domain,
        CancellationToken cancellationToken = default);
}
