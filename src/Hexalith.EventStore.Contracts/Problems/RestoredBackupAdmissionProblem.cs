using System;

using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.Contracts.Problems;

/// <summary>
/// Story 22.7c — stable ProblemDetails contract for restored-backup admission conflicts. Extension
/// keys here MUST be the only sources of detail surfaced in API/admin/CLI/MCP responses; payload
/// bytes, snapshot state, raw keys, provider-private metadata, and stack traces never appear.
/// </summary>
public static class RestoredBackupAdmissionProblem {
    /// <summary>Stable ProblemDetails type URI.</summary>
    public const string TypeUri = "https://hexalith.io/problems/restored-backup-admission-conflict";

    /// <summary>Default human-readable title.</summary>
    public const string DefaultTitle = "Restored backup admission conflict";

    /// <summary>Extension key carrying the admission identifier.</summary>
    public const string ExtensionAdmissionId = "admissionId";

    /// <summary>Extension key carrying the current admission state name.</summary>
    public const string ExtensionAdmissionState = "admissionState";

    /// <summary>Extension key carrying the stable reason code.</summary>
    public const string ExtensionReasonCode = "reasonCode";

    /// <summary>Extension key carrying the operator next-action hint.</summary>
    public const string ExtensionNextAction = "nextAction";

    /// <summary>Extension key carrying the affected tenant.</summary>
    public const string ExtensionTenantId = "tenantId";

    /// <summary>Extension key carrying the affected domain.</summary>
    public const string ExtensionDomain = "domain";

    /// <summary>Extension key carrying the backup manifest identifier.</summary>
    public const string ExtensionBackupManifestId = "backupManifestId";

    /// <summary>Extension key carrying the protection metadata schema version.</summary>
    public const string ExtensionMetadataVersion = "metadataVersion";

    /// <summary>Extension key carrying the watermark conflict description.</summary>
    public const string ExtensionWatermarkConflict = "watermarkConflict";

    /// <summary>Extension key carrying the correlation identifier.</summary>
    public const string ExtensionCorrelationId = "correlationId";

    /// <summary>Extension key carrying the audit identifier.</summary>
    public const string ExtensionAuditId = "auditId";

    /// <summary>Returns the recommended HTTP status code for the supplied admission state.</summary>
    /// <param name="state">The admission state.</param>
    /// <returns>The recommended HTTP status code.</returns>
    public static int GetStatusCode(RestoredBackupAdmissionState state) => state switch {
        RestoredBackupAdmissionState.Accepted => 200,
        RestoredBackupAdmissionState.Blocked => 409,
        RestoredBackupAdmissionState.Quarantined => 409,
        RestoredBackupAdmissionState.OperatorDecisionRequired => 409,
        RestoredBackupAdmissionState.DeferredValidation => 503,
        RestoredBackupAdmissionState.Pending => 202,
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown RestoredBackupAdmissionState value."),
    };

    /// <summary>Returns safe operator-facing guidance for the supplied admission state.</summary>
    /// <param name="state">The admission state.</param>
    /// <returns>A safe fixed guidance string.</returns>
    public static string GetSafeOperatorGuidance(RestoredBackupAdmissionState state) => state switch {
        RestoredBackupAdmissionState.Accepted => "Restored backup accepted. Protected data may be served.",
        RestoredBackupAdmissionState.Blocked => "Restored backup conflicts with an irreversible workflow. Reading protected data is blocked.",
        RestoredBackupAdmissionState.Quarantined => "Restored backup quarantined pending inspection. Submit an operator decision to close.",
        RestoredBackupAdmissionState.OperatorDecisionRequired => "Restored backup requires an explicit operator decision before any protected content can be served.",
        RestoredBackupAdmissionState.DeferredValidation => "Admission cannot be proved with current evidence. Provide additional restore evidence and retry.",
        RestoredBackupAdmissionState.Pending => "Admission recorded but no decision has been made yet.",
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown RestoredBackupAdmissionState value."),
    };
}
