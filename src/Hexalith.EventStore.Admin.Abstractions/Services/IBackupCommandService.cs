using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Abstractions.Models.Storage;
using Hexalith.EventStore.Contracts.Security;

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

    /// <summary>
    /// Story 22.7c — submits a restored-backup admission request. The admission result reports
    /// whether the restored data may be served, must be blocked/quarantined, requires explicit
    /// operator decision, or cannot be validated yet. Implementations MUST compare only safe
    /// metadata (manifest identity, tenant/domain/aggregate scope, sequence range, protection
    /// metadata version, key alias fingerprint) and MUST NOT inspect payload bytes, raw key
    /// material, or provider-private metadata.
    /// </summary>
    /// <param name="request">The admission request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A safe admission result.</returns>
    Task<RestoredBackupAdmissionResult> AdmitRestoredBackupAsync(
        RestoredBackupAdmissionRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Story 22.7c — records an explicit operator decision (accept, block, or quarantine) for an
    /// admission that previously returned <see cref="RestoredBackupAdmissionState.OperatorDecisionRequired"/>,
    /// <see cref="RestoredBackupAdmissionState.Quarantined"/>, or
    /// <see cref="RestoredBackupAdmissionState.DeferredValidation"/>. The operation is idempotent
    /// and scoped to the admission identifier.
    /// </summary>
    /// <param name="tenantId">The tenant scope of the admission.</param>
    /// <param name="admissionId">The admission identifier.</param>
    /// <param name="decision">The new admission state (must satisfy
    /// <see cref="RestoredBackupAdmissionTransitions.IsAllowed"/>).</param>
    /// <param name="operatorActorId">The operator submitting the decision.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated admission result.</returns>
    Task<RestoredBackupAdmissionResult> SubmitRestoreAdmissionDecisionAsync(
        string tenantId,
        string admissionId,
        RestoredBackupAdmissionState decision,
        string operatorActorId,
        CancellationToken ct = default);

    /// <summary>
    /// Story 22.7c — records an operator-initiated crypto-shredding workflow request using the
    /// provider-neutral workflow contract. Provider execution remains outside this boundary; the
    /// returned decision is the auditable EventStore workflow status.
    /// </summary>
    /// <param name="request">The workflow request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The recorded workflow decision.</returns>
    Task<CryptoShreddingWorkflowDecision> SubmitCryptoShreddingWorkflowAsync(
        CryptoShreddingWorkflowRequest request,
        CancellationToken ct = default);
}
