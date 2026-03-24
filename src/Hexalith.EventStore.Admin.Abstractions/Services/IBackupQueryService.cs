using Hexalith.EventStore.Admin.Abstractions.Models.Storage;

namespace Hexalith.EventStore.Admin.Abstractions.Services;

/// <summary>
/// Service interface for querying backup job history.
/// </summary>
public interface IBackupQueryService
{
    /// <summary>
    /// Gets backup jobs, optionally filtered by tenant.
    /// </summary>
    /// <param name="tenantId">Optional tenant filter.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of backup jobs.</returns>
    Task<IReadOnlyList<BackupJob>> GetBackupJobsAsync(string? tenantId, CancellationToken ct = default);
}
