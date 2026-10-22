namespace Hexalith.EventStore.Admin.Abstractions.Models.Storage;

/// <summary>
/// Represents a backup job with its status and metrics.
/// </summary>
/// <param name="BackupId">Unique identifier for the backup.</param>
/// <param name="TenantId">Tenant that was backed up.</param>
/// <param name="StreamId">Specific stream ID if stream-level backup, or null for full tenant backup.</param>
/// <param name="Description">Optional description/purpose of the backup.</param>
/// <param name="JobType">Whether this is a backup or restore operation.</param>
/// <param name="Status">Current job status.</param>
/// <param name="IncludeSnapshots">Whether snapshot state was included.</param>
/// <param name="CreatedAtUtc">When the backup was triggered.</param>
/// <param name="CompletedAtUtc">When the backup finished, or null if still running.</param>
/// <param name="EventCount">Number of events in backup, or null if not yet available.</param>
/// <param name="SizeBytes">Backup size in bytes, or null if not yet available.</param>
/// <param name="IsValidated">Whether integrity validation has been completed.</param>
/// <param name="ErrorMessage">Error details when status is Failed, otherwise null.</param>
public record BackupJob(
    string BackupId,
    string TenantId,
    string? StreamId,
    string? Description,
    BackupJobType JobType,
    BackupJobStatus Status,
    bool IncludeSnapshots,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    long? EventCount,
    long? SizeBytes,
    bool IsValidated,
    string? ErrorMessage);
