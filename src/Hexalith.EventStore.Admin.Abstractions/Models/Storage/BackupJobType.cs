namespace Hexalith.EventStore.Admin.Abstractions.Models.Storage;

/// <summary>
/// Distinguishes backup jobs from restore jobs.
/// </summary>
public enum BackupJobType
{
    /// <summary>A backup operation (snapshot of event data).</summary>
    Backup,

    /// <summary>A restore operation (re-injection from a backup).</summary>
    Restore,
}
