using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.EventStore.Contracts.Commands;
/// <summary>
/// Snapshot-aware aggregate state payload passed to domain services.
/// Carries an optional snapshot state plus the tail events needed to reach the current sequence.
/// </summary>
/// <param name="SnapshotState">The snapshot state payload, or null when no snapshot was used.</param>
/// <param name="Events">The tail events after the snapshot sequence, or the full event stream when no snapshot exists.</param>
/// <param name="LastSnapshotSequence">The snapshot sequence number, or 0 when no snapshot exists.</param>
/// <param name="CurrentSequence">The latest aggregate sequence number.</param>
public sealed record DomainServiceCurrentState(
    object? SnapshotState,
    IReadOnlyList<EventEnvelope> Events,
    long LastSnapshotSequence,
    long CurrentSequence)
{
    /// <summary>Gets the number of events carried in this payload.</summary>
    public int EventCount => Events.Count;

    /// <summary>Gets whether a snapshot was used for this rehydration payload.</summary>
    public bool UsedSnapshot => SnapshotState is not null;
}