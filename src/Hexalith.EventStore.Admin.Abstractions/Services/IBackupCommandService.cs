using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Abstractions.Models.Storage;

namespace Hexalith.EventStore.Admin.Abstractions.Services;

/// <summary>
/// Service interface for admin-level backup and restore operations.
/// </summary>
public interface IBackupCommandService {
    /// <summary>
    /// Triggers a full tenant backup.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="description">Optional description/purpose of the backup.</param>
    /// <param name="includeSnapshots">Whether to include snapshot state.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The operation result.</returns>
    Task<AdminOperationResult> TriggerBackupAsync(string tenantId, string? description, bool includeSnapshots, CancellationToken ct = default);

    /// <summary>
    /// Validates integrity of a completed backup.
    /// </summary>
    /// <param name="backupId">The backup identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The operation result.</returns>
    Task<AdminOperationResult> ValidateBackupAsync(string backupId, CancellationToken ct = default);

    /// <summary>
    /// Initiates a restore from a backup.
    /// </summary>
    /// <param name="backupId">The backup identifier.</param>
    /// <param name="pointInTime">Optional cutoff timestamp for point-in-time restore.</param>
    /// <param name="dryRun">Whether to validate without applying.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The operation result.</returns>
    Task<AdminOperationResult> TriggerRestoreAsync(string backupId, DateTimeOffset? pointInTime, bool dryRun, CancellationToken ct = default);

    /// <summary>
    /// Exports a single stream as downloadable content.
    /// </summary>
    /// <param name="request">The export request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The export result with content.</returns>
    Task<StreamExportResult> ExportStreamAsync(StreamExportRequest request, CancellationToken ct = default);

    /// <summary>
    /// Imports events into a stream from exported content.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="content">The exported content to import.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The operation result.</returns>
    Task<AdminOperationResult> ImportStreamAsync(string tenantId, string content, CancellationToken ct = default);
}
