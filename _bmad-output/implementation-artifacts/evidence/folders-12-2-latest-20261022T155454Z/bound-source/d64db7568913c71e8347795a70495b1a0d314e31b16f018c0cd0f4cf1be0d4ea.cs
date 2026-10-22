namespace Hexalith.EventStore.Admin.Abstractions.Models.Storage;

/// <summary>
/// Represents the status of a compaction job.
/// </summary>
public enum CompactionJobStatus {
    /// <summary>The compaction job is queued and waiting to start.</summary>
    Pending,

    /// <summary>The compaction job is currently running.</summary>
    Running,

    /// <summary>The compaction job completed successfully.</summary>
    Completed,

    /// <summary>The compaction job failed.</summary>
    Failed,
}
