namespace Hexalith.EventStore.Admin.Abstractions.Models.Consistency;

/// <summary>
/// Types of consistency checks that can be performed on the event store.
/// </summary>
public enum ConsistencyCheckType
{
    /// <summary>Verifies events exist at sequential positions (1, 2, 3, ..., N) with no gaps.</summary>
    SequenceContinuity,

    /// <summary>Verifies snapshot sequence number is valid and does not exceed the latest event sequence.</summary>
    SnapshotIntegrity,

    /// <summary>Verifies projection last-processed positions are not ahead of actual event counts.</summary>
    ProjectionPositions,

    /// <summary>Verifies aggregate metadata sequence count matches the actual stored event count.</summary>
    MetadataConsistency,
}
