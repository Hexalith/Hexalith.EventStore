namespace Hexalith.EventStore.Server.Events;

using Dapr.Actors.Runtime;

using Hexalith.EventStore.Contracts.Identity;

/// <summary>
/// Manages aggregate state snapshots for optimizing state rehydration.
/// Snapshots are an advisory optimization -- failures never block command processing (rule #12).
/// </summary>
public interface ISnapshotManager {
    /// <summary>
    /// Determines whether a snapshot should be created based on the configured interval.
    /// </summary>
    /// <param name="domain">The domain name (used to resolve per-domain interval overrides).</param>
    /// <param name="currentSequence">The current event sequence number after persistence.</param>
    /// <param name="lastSnapshotSequence">The sequence number of the last snapshot (0 if none).</param>
    /// <returns><c>true</c> if a snapshot should be created; otherwise <c>false</c>.</returns>
    Task<bool> ShouldCreateSnapshotAsync(string domain, long currentSequence, long lastSnapshotSequence);

    /// <summary>
    /// Creates a snapshot by staging it via IActorStateManager.SetStateAsync.
    /// Does NOT call SaveStateAsync -- the caller commits atomically (D1).
    /// On failure, logs a warning and returns without throwing (advisory per rule #12).
    /// </summary>
    /// <param name="identity">The aggregate identity providing key derivation.</param>
    /// <param name="sequenceNumber">The event sequence number this snapshot represents.</param>
    /// <param name="state">The aggregate state to snapshot (domain-specific, opaque to EventStore).</param>
    /// <param name="stateManager">The actor state manager for staging the snapshot write.</param>
    /// <param name="correlationId">The correlation ID for structured logging (rule #9). Optional.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task CreateSnapshotAsync(AggregateIdentity identity, long sequenceNumber, object state, IActorStateManager stateManager, string? correlationId = null);

    /// <summary>
    /// Loads an existing snapshot for an aggregate.
    /// Returns null if no snapshot exists or if deserialization fails (graceful degradation).
    /// On deserialization failure, deletes the corrupt snapshot and logs a warning.
    /// </summary>
    /// <param name="identity">The aggregate identity providing key derivation.</param>
    /// <param name="stateManager">The actor state manager for reading the snapshot.</param>
    /// <param name="correlationId">The correlation ID for structured logging (rule #9). Optional.</param>
    /// <returns>The snapshot record, or null if no valid snapshot exists.</returns>
    Task<SnapshotRecord?> LoadSnapshotAsync(AggregateIdentity identity, IActorStateManager stateManager, string? correlationId = null);
}
