namespace Hexalith.EventStore.Server.Commands;

/// <summary>
/// Tracks in-flight command counts per aggregate and enforces backpressure thresholds (FR67).
/// When the number of pending commands for an aggregate exceeds the configured limit,
/// new commands are rejected before entering the processing pipeline.
/// </summary>
public interface IBackpressureTracker {
    /// <summary>
    /// Attempts to acquire a slot for the given aggregate. Returns true if under the threshold,
    /// false if the aggregate has reached its backpressure limit. The caller MUST call
    /// <see cref="Release"/> when the command completes (success, failure, or exception)
    /// if and only if this method returned true.
    /// </summary>
    /// <param name="aggregateActorId">The canonical actor ID for the aggregate.</param>
    /// <returns>True if a slot was acquired; false if backpressure threshold is exceeded.</returns>
    bool TryAcquire(string aggregateActorId);

    /// <summary>
    /// Returns the current in-flight command depth for an aggregate.
    /// This is primarily for diagnostics and logging.
    /// </summary>
    /// <param name="aggregateActorId">The canonical actor ID for the aggregate.</param>
    /// <returns>The current in-flight depth for the aggregate.</returns>
    int GetCurrentDepth(string aggregateActorId);

    /// <summary>
    /// Releases a previously acquired slot for the given aggregate, decrementing the in-flight counter.
    /// Must only be called after a successful <see cref="TryAcquire"/> call (returned true).
    /// </summary>
    /// <param name="aggregateActorId">The canonical actor ID for the aggregate.</param>
    void Release(string aggregateActorId);
}
