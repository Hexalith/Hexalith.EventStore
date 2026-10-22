namespace Hexalith.EventStore.Client.Projections;

/// <summary>Durable state of a marker-gated staged read-model batch.</summary>
public enum ReadModelBatchStagingStatus {
    /// <summary>All candidates are installed but the previous view remains visible.</summary>
    Prepared = 0,

    /// <summary>The commit marker and every candidate value were read back successfully.</summary>
    Committed = 1,

    /// <summary>The previous complete view was restored.</summary>
    Aborted = 2,

    /// <summary>The stable identity conflicts with another manifest or optimistic write.</summary>
    Conflict = 3,

    /// <summary>The durable state could not be proven within the bounded reconciliation attempt.</summary>
    Indeterminate = 4,
}
