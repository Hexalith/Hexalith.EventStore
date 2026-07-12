namespace Hexalith.EventStore.Client.Projections;

/// <summary>
/// The durable state-machine status of a coordinated batch marker.
/// </summary>
internal enum ReadModelBatchMarkerStatus {
    /// <summary>Identity and fingerprint recorded; pending operations may be installed.</summary>
    Prepared = 0,

    /// <summary>The visibility decision was made: all operations are durable and visible.</summary>
    Committed = 1,

    /// <summary>A pre-commit conflict is being compensated; the previous view is being restored.</summary>
    Aborting = 2,

    /// <summary>Compensation finished; the previous view is restored. The identity may be retried.</summary>
    Aborted = 3,

    /// <summary>Terminal completion receipt after envelope compaction.</summary>
    Completed = 4,
}
