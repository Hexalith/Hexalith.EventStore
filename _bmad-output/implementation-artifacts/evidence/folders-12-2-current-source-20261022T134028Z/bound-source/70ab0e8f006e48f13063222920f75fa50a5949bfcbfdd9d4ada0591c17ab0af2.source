namespace Hexalith.EventStore.Contracts.Security;

/// <summary>
/// Story 22.7c — provider-neutral request for restored-backup admission. Compares safe metadata
/// only: tenant, domain, aggregate or stream pattern, sequence range, protection metadata version,
/// key alias fingerprint policy, deletion/invalidation watermark, backup creation time, restore
/// time, and manifest identity. Raw key aliases, payload bytes, snapshot state, IVs/nonces,
/// provider-private metadata, state-store keys, and connection strings are NEVER permitted.
/// </summary>
/// <param name="AdmissionId">Caller-supplied stable ULID for the admission request.</param>
/// <param name="TenantId">Tenant scope.</param>
/// <param name="Domain">Domain scope.</param>
/// <param name="AggregateId">Optional aggregate identifier.</param>
/// <param name="FromSequence">Optional inclusive lower bound of affected sequence range.</param>
/// <param name="ToSequence">Optional inclusive upper bound of affected sequence range.</param>
/// <param name="BackupManifestId">Identifier of the backup manifest under admission.</param>
/// <param name="BackupCreatedAtUtc">When the backup was created.</param>
/// <param name="RestoreRequestedAtUtc">When the restore was requested.</param>
/// <param name="ProtectionMetadataVersion">Protection metadata schema version observed in the manifest.</param>
/// <param name="KeyReferencePolicy">Policy controlling whether a key reference is recorded.</param>
/// <param name="KeyAliasFingerprint">Optional SHA-256 hex prefix of the key alias.</param>
/// <param name="DeletionWatermarkUtc">Optional crypto-shredding decision watermark to compare against.</param>
/// <param name="CorrelationId">Optional correlation identifier.</param>
/// <param name="OperatorActorId">Identifier of the operator/process submitting the admission.</param>
public sealed record RestoredBackupAdmissionRequest(
    string AdmissionId,
    string TenantId,
    string Domain,
    string? AggregateId,
    long? FromSequence,
    long? ToSequence,
    string BackupManifestId,
    DateTimeOffset BackupCreatedAtUtc,
    DateTimeOffset RestoreRequestedAtUtc,
    int ProtectionMetadataVersion,
    KeyReferencePolicy KeyReferencePolicy,
    string? KeyAliasFingerprint,
    DateTimeOffset? DeletionWatermarkUtc,
    string? CorrelationId,
    string OperatorActorId) {
    /// <summary>Maximum identifier length (ULID).</summary>
    public const int MaxIdentifierLength = 64;

    /// <summary>Maximum manifest identifier length.</summary>
    public const int MaxManifestIdLength = 128;

    /// <summary>Validates the request shape.</summary>
    /// <param name="rejectionReason">A short human-readable rejection reason when validation fails.</param>
    /// <returns><see langword="true"/> when the request is valid.</returns>
    public bool TryValidate(out string? rejectionReason) {
        if (string.IsNullOrWhiteSpace(AdmissionId) || AdmissionId.Length > MaxIdentifierLength) {
            rejectionReason = "AdmissionId is required and bounded.";
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

        if (string.IsNullOrWhiteSpace(BackupManifestId) || BackupManifestId.Length > MaxManifestIdLength) {
            rejectionReason = "BackupManifestId is required and bounded.";
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

        if (ProtectionMetadataVersion < 1) {
            rejectionReason = "ProtectionMetadataVersion must be >= 1.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(OperatorActorId)) {
            rejectionReason = "OperatorActorId is required.";
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
