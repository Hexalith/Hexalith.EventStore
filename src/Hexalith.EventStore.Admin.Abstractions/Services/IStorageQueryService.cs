using Hexalith.EventStore.Admin.Abstractions.Models.Storage;

namespace Hexalith.EventStore.Admin.Abstractions.Services;

/// <summary>
/// Service interface for querying storage usage and snapshot policies (FR76).
/// </summary>
public interface IStorageQueryService {
    /// <summary>
    /// Gets the storage overview, optionally filtered by tenant.
    /// </summary>
    /// <param name="tenantId">Optional tenant filter.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The storage overview.</returns>
    Task<StorageOverview> GetStorageOverviewAsync(string? tenantId, CancellationToken ct = default);

    /// <summary>
    /// Gets the streams with the highest storage usage.
    /// </summary>
    /// <param name="tenantId">Optional tenant filter.</param>
    /// <param name="count">Maximum number of streams to return.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of stream storage information.</returns>
    Task<IReadOnlyList<StreamStorageInfo>> GetHotStreamsAsync(string? tenantId, int count, CancellationToken ct = default);

    /// <summary>
    /// Gets the snapshot policies, optionally filtered by tenant.
    /// </summary>
    /// <param name="tenantId">Optional tenant filter.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of snapshot policies.</returns>
    Task<IReadOnlyList<SnapshotPolicy>> GetSnapshotPoliciesAsync(string? tenantId, CancellationToken ct = default);

    /// <summary>
    /// Gets the compaction jobs, optionally filtered by tenant.
    /// </summary>
    /// <param name="tenantId">Optional tenant filter.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of compaction jobs.</returns>
    Task<IReadOnlyList<CompactionJob>> GetCompactionJobsAsync(string? tenantId, CancellationToken ct = default);
}
