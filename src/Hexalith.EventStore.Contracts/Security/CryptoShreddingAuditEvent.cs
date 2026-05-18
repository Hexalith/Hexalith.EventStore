using System;

namespace Hexalith.EventStore.Contracts.Security;

/// <summary>
/// Story 22.7c — auditable record describing a single workflow or restore-admission decision.
/// Every transition appends one of these records. The record carries only safe fields — payload
/// bytes, snapshot state, raw keys, IVs/nonces, authentication tags, provider-private metadata,
/// stack traces, state-store keys, and connection strings are forbidden.
/// </summary>
/// <param name="AuditId">Stable ULID for this audit record.</param>
/// <param name="WorkflowId">The workflow this audit belongs to (when applicable).</param>
/// <param name="AdmissionId">The admission record this audit belongs to (when applicable).</param>
/// <param name="TenantId">Tenant scope.</param>
/// <param name="Domain">Domain scope.</param>
/// <param name="AggregateId">Optional aggregate identifier.</param>
/// <param name="FromSequence">Optional inclusive lower bound of affected sequence range.</param>
/// <param name="ToSequence">Optional inclusive upper bound of affected sequence range.</param>
/// <param name="ProtectionMetadataVersion">Protection metadata schema version observed.</param>
/// <param name="KeyReferencePolicy">Policy controlling whether a key reference is recorded.</param>
/// <param name="KeyAliasFingerprint">Optional SHA-256 hex prefix of the key alias.</param>
/// <param name="WorkflowFromState">Source workflow state (null when audit is admission-only).</param>
/// <param name="WorkflowToState">Target workflow state (null when audit is admission-only).</param>
/// <param name="AdmissionFromState">Source admission state (null when audit is workflow-only).</param>
/// <param name="AdmissionToState">Target admission state (null when audit is workflow-only).</param>
/// <param name="DecisionActorId">The actor who recorded the decision.</param>
/// <param name="CorrelationId">Optional correlation identifier.</param>
/// <param name="DecidedAtUtc">When the decision was recorded.</param>
/// <param name="ReasonCode">Stable kebab-case reason code.</param>
public sealed record CryptoShreddingAuditEvent(
    string AuditId,
    string? WorkflowId,
    string? AdmissionId,
    string TenantId,
    string Domain,
    string? AggregateId,
    long? FromSequence,
    long? ToSequence,
    int ProtectionMetadataVersion,
    KeyReferencePolicy KeyReferencePolicy,
    string? KeyAliasFingerprint,
    CryptoShreddingWorkflowState? WorkflowFromState,
    CryptoShreddingWorkflowState? WorkflowToState,
    RestoredBackupAdmissionState? AdmissionFromState,
    RestoredBackupAdmissionState? AdmissionToState,
    string DecisionActorId,
    string? CorrelationId,
    DateTimeOffset DecidedAtUtc,
    string ReasonCode) {
    /// <summary>Validates the audit record's structural invariants.</summary>
    /// <param name="rejectionReason">A short human-readable rejection reason when validation fails.</param>
    /// <returns><see langword="true"/> when the record is valid.</returns>
    public bool TryValidate(out string? rejectionReason) {
        if (string.IsNullOrWhiteSpace(AuditId)) {
            rejectionReason = "AuditId is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(TenantId)) {
            rejectionReason = "TenantId is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(Domain)) {
            rejectionReason = "Domain is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(DecisionActorId)) {
            rejectionReason = "DecisionActorId is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(ReasonCode)) {
            rejectionReason = "ReasonCode is required.";
            return false;
        }

        bool hasWorkflowTransition = WorkflowToState.HasValue;
        bool hasAdmissionTransition = AdmissionToState.HasValue;
        if (!hasWorkflowTransition && !hasAdmissionTransition) {
            rejectionReason = "Audit record must describe a workflow or admission transition.";
            return false;
        }

        if (hasWorkflowTransition && string.IsNullOrWhiteSpace(WorkflowId)) {
            rejectionReason = "WorkflowId is required for workflow transitions.";
            return false;
        }

        if (hasAdmissionTransition && string.IsNullOrWhiteSpace(AdmissionId)) {
            rejectionReason = "AdmissionId is required for admission transitions.";
            return false;
        }

        if (ProtectionMetadataVersion < 1) {
            rejectionReason = "ProtectionMetadataVersion must be >= 1.";
            return false;
        }

        if (FromSequence.HasValue && FromSequence.Value < 0) {
            rejectionReason = "FromSequence must be non-negative.";
            return false;
        }

        if (ToSequence.HasValue && ToSequence.Value < 0) {
            rejectionReason = "ToSequence must be non-negative.";
            return false;
        }

        if (FromSequence.HasValue && ToSequence.HasValue && ToSequence.Value < FromSequence.Value) {
            rejectionReason = "ToSequence must be >= FromSequence when both are set.";
            return false;
        }

        if (KeyReferencePolicy != KeyReferencePolicy.NoKeyReference) {
            if (string.IsNullOrWhiteSpace(KeyAliasFingerprint)
                || KeyAliasFingerprint.Length != CryptoShreddingWorkflowIdentity.KeyAliasFingerprintLength) {
                rejectionReason = "KeyAliasFingerprint must be a 16-character hex string when policy allows a reference.";
                return false;
            }

            for (int i = 0; i < KeyAliasFingerprint.Length; i++) {
                char c = KeyAliasFingerprint[i];
                bool isHex = c is (>= '0' and <= '9') or (>= 'a' and <= 'f');
                if (!isHex) {
                    rejectionReason = "KeyAliasFingerprint must be lowercase hex.";
                    return false;
                }
            }
        }
        else if (!string.IsNullOrEmpty(KeyAliasFingerprint)) {
            rejectionReason = "KeyAliasFingerprint must be empty when policy is NoKeyReference.";
            return false;
        }

        rejectionReason = null;
        return true;
    }
}
