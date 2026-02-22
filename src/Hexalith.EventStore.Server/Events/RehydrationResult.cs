namespace Hexalith.EventStore.Server.Events;

/// <summary>
/// Result of aggregate state rehydration, separating snapshot state from tail events.
/// EventStore is schema-ignorant: it cannot apply tail events to snapshot state.
/// Only the domain service knows its own state shape and can project events onto it.
/// </summary>
/// <param name="SnapshotState">Opaque domain state from the snapshot, or null if no snapshot was used.</param>
/// <param name="Events">Tail events after the snapshot sequence, or ALL events if no snapshot was used.</param>
/// <param name="LastSnapshotSequence">The snapshot's SequenceNumber, or 0 if no snapshot.</param>
/// <param name="CurrentSequence">The highest event sequence number from metadata.</param>
public record RehydrationResult(
    object? SnapshotState,
    List<EventEnvelope> Events,
    long LastSnapshotSequence,
    long CurrentSequence) {
    /// <summary>Gets the number of tail events (for diagnostics/logging).</summary>
    public int TailEventCount => Events.Count;

    /// <summary>Gets whether a snapshot was used during rehydration.</summary>
    public bool UsedSnapshot => SnapshotState is not null;
}
