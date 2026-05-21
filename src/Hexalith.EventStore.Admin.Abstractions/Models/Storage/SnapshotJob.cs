namespace Hexalith.EventStore.Admin.Abstractions.Models.Storage;

/// <summary>
/// Represents a manual snapshot creation job with its status, identity tuple, and outcome metadata.
/// </summary>
/// <remarks>
/// Aligned in shape with <see cref="CompactionJob"/>. The job record is operational evidence written
/// to <c>admin:storage-snapshot-jobs:all</c> and <c>admin:storage-snapshot-jobs:{tenantId}</c>; it is
/// NOT the source of truth for snapshot existence (the actor state store snapshot key is).
/// </remarks>
/// <param name="OperationId">Deterministic, sequence-scoped operation identifier. See DW16 story §Implementation Decisions.</param>
/// <param name="TenantId">Canonical (lower-cased) tenant identifier.</param>
/// <param name="Domain">Canonical (lower-cased) domain name.</param>
/// <param name="AggregateId">Aggregate identifier (case-sensitive).</param>
/// <param name="SequenceNumber">The stream sequence number that was snapshotted (or attempted).</param>
/// <param name="Status">Current job status.</param>
/// <param name="StartedAtUtc">When the job was accepted.</param>
/// <param name="CompletedAtUtc">When the job reached a terminal state, or null while in flight.</param>
/// <param name="SnapshotKey">The state-store snapshot key, when known.</param>
/// <param name="ErrorCode">A safe, stable failure-reason code; null on success.</param>
/// <param name="ErrorMessage">A safe operator-facing failure message; null on success.</param>
public record SnapshotJob(
    string OperationId,
    string TenantId,
    string Domain,
    string AggregateId,
    long SequenceNumber,
    SnapshotJobStatus Status,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    string? SnapshotKey,
    string? ErrorCode,
    string? ErrorMessage);
