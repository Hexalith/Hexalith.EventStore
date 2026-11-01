namespace Hexalith.EventStore.Client.Projections.Checkpointed;

/// <summary>
/// The single persisted record of a checkpointed projection: state, health, contiguous checkpoint, gap
/// horizon, sequence-keyed pending evidence, and the recovery lease, all covered by one compare-and-swap.
/// </summary>
/// <typeparam name="TState">The projection state type.</typeparam>
public sealed record CheckpointedProjectionRecord<TState>
    where TState : class {
    /// <summary>The current record schema version.</summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>Gets the record schema version.</summary>
    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    /// <summary>Gets the projected state at <see cref="LastContiguousSequence"/>.</summary>
    public TState? State { get; init; }

    /// <summary>Gets the persisted projection health.</summary>
    public CheckpointedProjectionHealth Health { get; init; } = CheckpointedProjectionHealth.Healthy;

    /// <summary>Gets the highest aggregate sequence applied contiguously from the first event.</summary>
    public long LastContiguousSequence { get; init; }

    /// <summary>Gets the accepted identity at <see cref="LastContiguousSequence"/>, when one was applied.</summary>
    public CheckpointedProjectionEvidence? LastAccepted { get; init; }

    /// <summary>Gets the immutable first missing sequence while the record is gapped.</summary>
    public long? Expected { get; init; }

    /// <summary>Gets the monotonic recovery horizon (highest observed future sequence) while gapped.</summary>
    public long? RecoveryThrough { get; init; }

    /// <summary>Gets the durable observed evidence for future deliveries, ordered by sequence.</summary>
    public IReadOnlyList<CheckpointedProjectionPendingEvidence> Pending { get; init; } = [];

    /// <summary>Gets the current recovery lease, when one was granted and not yet released.</summary>
    public CheckpointedProjectionLease? Lease { get; init; }

    /// <summary>Gets the highest fencing token ever granted for this record.</summary>
    public long LastFencingToken { get; init; }

    /// <summary>Gets the monotonic record version, incremented by every committed write.</summary>
    public long Version { get; init; }

    /// <summary>Gets the content-free reason the record became corrupt, when it is corrupt.</summary>
    public CheckpointedProjectionCorruptionReason? CorruptionReason { get; init; }

    /// <summary>Gets the aggregate sequence that proved corruption, when it is corrupt.</summary>
    public long? CorruptSequence { get; init; }

    /// <summary>Gets a value indicating whether the record is healthy and therefore may authorize.</summary>
    public bool IsHealthy => Health == CheckpointedProjectionHealth.Healthy;

    /// <summary>
    /// Validates the structural invariants of the record without trusting its state contents.
    /// </summary>
    /// <returns><see langword="true"/> when the record is well formed.</returns>
    public bool IsWellFormed() {
        if (SchemaVersion != CurrentSchemaVersion
            || LastContiguousSequence < 0
            || Version < 0
            || LastFencingToken < 0
            || Pending is null
            || (LastContiguousSequence == 0) != (LastAccepted is null)
            || (LastAccepted is not null && !LastAccepted.IsComplete)) {
            return false;
        }

        if (Lease is { } lease
            && (string.IsNullOrWhiteSpace(lease.WorkerId) || lease.FencingToken < 1 || lease.FencingToken > LastFencingToken)) {
            return false;
        }

        return Health switch {
            CheckpointedProjectionHealth.Healthy => Expected is null && RecoveryThrough is null && Pending.Count == 0
                && Lease is null && CorruptionReason is null,
            CheckpointedProjectionHealth.Gapped => IsWellFormedGap(),
            CheckpointedProjectionHealth.Corrupt => CorruptionReason is not null && Lease is null,
            _ => false,
        };
    }

    private bool IsWellFormedGap() {
        if (Expected is not { } expected
            || RecoveryThrough is not { } through
            || expected != LastContiguousSequence + 1
            || through < expected
            || Pending.Count == 0
            || CorruptionReason is not null) {
            return false;
        }

        long previous = LastContiguousSequence;
        foreach (CheckpointedProjectionPendingEvidence pending in Pending) {
            if (pending?.Evidence is null
                || !pending.Evidence.IsComplete
                || pending.Sequence <= previous
                || pending.Sequence < expected
                || pending.Sequence > through) {
                return false;
            }

            previous = pending.Sequence;
        }

        return previous == through;
    }
}
