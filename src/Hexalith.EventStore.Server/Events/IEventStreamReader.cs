namespace Hexalith.EventStore.Server.Events;

using Hexalith.EventStore.Contracts.Identity;

/// <summary>
/// Reads events from the actor state store and rehydrates aggregate state.
/// Supports snapshot-aware rehydration: when a snapshot is provided, only tail events
/// after the snapshot sequence are loaded (Story 3.10).
/// </summary>
public interface IEventStreamReader {
    /// <summary>
    /// Rehydrates aggregate state by loading events from the event stream.
    /// When a snapshot is provided, loads only tail events after the snapshot sequence.
    /// When no snapshot is provided, performs a full replay from sequence 1.
    /// </summary>
    /// <param name="identity">The aggregate identity providing key derivation.</param>
    /// <param name="snapshot">Optional snapshot to use as the rehydration starting point.</param>
    /// <returns>
    /// A <see cref="RehydrationResult"/> containing snapshot state and/or events,
    /// or null for new aggregates with no events and no snapshot.
    /// </returns>
    Task<RehydrationResult?> RehydrateAsync(AggregateIdentity identity, SnapshotRecord? snapshot = null);
}
