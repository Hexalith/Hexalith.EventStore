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
    Task<ProjectionRebuildCheckpointSaveResult> SaveAsync(
        ProjectionRebuildCheckpointScope scope,
        long lastAppliedSequence,
        ProjectionRebuildStatus status,
        string? failureReasonCode = null,
        CancellationToken cancellationToken = default,
        long? toPosition = null);

    /// <summary>
    /// Explicitly rewinds projection rebuild progress with optimistic concurrency.
    /// </summary>
    /// <remarks>
    /// This method intentionally bypasses the monotonic progress and lifecycle protections used by
    /// <see cref="SaveAsync"/>. Callers must enforce operator authorization and only use it for
    /// explicit lifecycle commands such as reset, replay, or retry.
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
