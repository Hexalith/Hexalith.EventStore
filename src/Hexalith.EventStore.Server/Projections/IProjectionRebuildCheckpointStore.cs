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
    Task<ProjectionRebuildCheckpointSaveResult> ResetAsync(
        ProjectionRebuildCheckpointScope scope,
        long lastAppliedSequence,
        ProjectionRebuildStatus status,
        string? failureReasonCode = null,
        CancellationToken cancellationToken = default,
        long? toPosition = null);
}
