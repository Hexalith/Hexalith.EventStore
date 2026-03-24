namespace Hexalith.EventStore.Admin.Abstractions.Models.Storage;

/// <summary>
/// Represents the status of a backup job.
/// </summary>
public enum BackupJobStatus
{
    /// <summary>The backup job is queued and waiting to start.</summary>
    Pending,

    /// <summary>The backup job is currently running.</summary>
    Running,

    /// <summary>The backup job completed successfully.</summary>
    Completed,

    /// <summary>The backup job failed.</summary>
    Failed,

    /// <summary>The backup is being validated for integrity.</summary>
    Validating,
}
