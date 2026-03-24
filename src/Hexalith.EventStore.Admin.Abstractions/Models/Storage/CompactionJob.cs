namespace Hexalith.EventStore.Admin.Abstractions.Models.Storage;

/// <summary>
/// Represents a compaction job with its status and metrics.
/// </summary>
/// <param name="OperationId">Unique identifier from the trigger operation.</param>
/// <param name="TenantId">Tenant that was compacted.</param>
/// <param name="Domain">Domain scope, or null for all domains in tenant.</param>
/// <param name="Status">Current job status.</param>
/// <param name="StartedAtUtc">When the compaction was triggered.</param>
/// <param name="CompletedAtUtc">When the compaction finished, or null if still running.</param>
/// <param name="EventsCompacted">Number of events processed, or null if not yet available.</param>
/// <param name="SpaceReclaimedBytes">Bytes reclaimed, or null if backend doesn't support (NFR44).</param>
/// <param name="ErrorMessage">Error details when status is Failed, otherwise null.</param>
public record CompactionJob(
    string OperationId,
    string TenantId,
    string? Domain,
    CompactionJobStatus Status,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    long? EventsCompacted,
    long? SpaceReclaimedBytes,
    string? ErrorMessage);
