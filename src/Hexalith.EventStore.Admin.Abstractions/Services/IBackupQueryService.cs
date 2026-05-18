using Hexalith.EventStore.Admin.Abstractions.Models.Storage;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.Admin.Abstractions.Services;

/// <summary>
/// Service interface for querying backup job history.
/// </summary>
public interface IBackupQueryService {
    /// <summary>
    /// Gets backup jobs, optionally filtered by tenant.
    /// </summary>
    /// <param name="tenantId">Optional tenant filter.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of backup jobs.</returns>
    Task<IReadOnlyList<BackupJob>> GetBackupJobsAsync(string? tenantId, CancellationToken ct = default);

    /// <summary>
    /// Story 22.7c — gets the current admission status for a restored-backup admission record.
    /// Returns <see langword="null"/> when no admission record with the supplied identifier exists.
    /// The result carries only safe metadata; payload bytes, raw key material, provider-private
    /// metadata, and stack traces are never present.
    /// </summary>
    /// <param name="tenantId">The tenant scope for authorization and lookup.</param>
    /// <param name="admissionId">The admission identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The admission status, or <see langword="null"/> when not found.</returns>
    Task<RestoredBackupAdmissionResult?> GetRestoreAdmissionAsync(
        string tenantId,
        string admissionId,
        CancellationToken ct = default);

    /// <summary>
    /// Story 22.7c — gets the current status of a crypto-shredding workflow record.
    /// </summary>
    /// <param name="tenantId">The tenant scope for authorization and lookup.</param>
    /// <param name="workflowId">The workflow identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The workflow decision, or <see langword="null"/> when not found.</returns>
    Task<CryptoShreddingWorkflowDecision?> GetCryptoShreddingWorkflowAsync(
        string tenantId,
        string workflowId,
        CancellationToken ct = default);
}
