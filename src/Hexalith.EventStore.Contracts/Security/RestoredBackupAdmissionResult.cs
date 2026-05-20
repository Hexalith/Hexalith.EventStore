namespace Hexalith.EventStore.Contracts.Security;

/// <summary>
/// Story 22.7c — result of a restored-backup admission decision. Carries the state plus safe
/// envelope metadata only. Provider exception text, raw keys, payload bytes, and snapshot state
/// are never present.
/// </summary>
/// <param name="AdmissionId">Stable identifier of the admission record.</param>
/// <param name="State">The admission state.</param>
/// <param name="TenantId">Tenant scope.</param>
/// <param name="Domain">Domain scope.</param>
/// <param name="AggregateId">Optional aggregate identifier.</param>
/// <param name="FromSequence">Optional inclusive lower bound of affected sequence range.</param>
/// <param name="ToSequence">Optional inclusive upper bound of affected sequence range.</param>
/// <param name="BackupManifestId">The manifest under admission.</param>
/// <param name="ProtectionMetadataVersion">Protection metadata schema version observed.</param>
/// <param name="KeyReferencePolicy">Policy controlling whether a key reference is recorded.</param>
/// <param name="KeyAliasFingerprint">Optional SHA-256 hex prefix of the key alias.</param>
/// <param name="WatermarkConflict">Provider-neutral conflict description (kebab-case reason code).</param>
/// <param name="ReasonCode">Stable reason code (kebab-case).</param>
/// <param name="NextAction">Operator next action hint.</param>
/// <param name="CorrelationId">Optional correlation identifier.</param>
/// <param name="AuditId">Optional audit identifier referencing the stored decision.</param>
/// <param name="DecisionActorId">The actor who made the latest decision.</param>
/// <param name="DecidedAtUtc">When the latest decision was made.</param>
/// <param name="IdempotentReplay">Indicates whether this result was produced by a repeated request.</param>
public sealed record RestoredBackupAdmissionResult(
    string AdmissionId,
    RestoredBackupAdmissionState State,
    string TenantId,
    string Domain,
    string? AggregateId,
    long? FromSequence,
    long? ToSequence,
    string BackupManifestId,
    int ProtectionMetadataVersion,
    KeyReferencePolicy KeyReferencePolicy,
    string? KeyAliasFingerprint,
    string? WatermarkConflict,
    string ReasonCode,
    CryptoShreddingNextAction NextAction,
    string? CorrelationId,
    string? AuditId,
    string DecisionActorId,
    DateTimeOffset DecidedAtUtc,
    bool IdempotentReplay) {
    /// <summary>Stable reason code emitted for <see cref="RestoredBackupAdmissionState.Accepted"/>.</summary>
    public const string AcceptedCode = "accepted";

    /// <summary>Stable reason code emitted for <see cref="RestoredBackupAdmissionState.Blocked"/>.</summary>
    public const string BlockedCode = "blocked";

    /// <summary>Stable reason code emitted for <see cref="RestoredBackupAdmissionState.Quarantined"/>.</summary>
    public const string QuarantinedCode = "quarantined";

    /// <summary>Stable reason code emitted for <see cref="RestoredBackupAdmissionState.OperatorDecisionRequired"/>.</summary>
    public const string OperatorDecisionRequiredCode = "operator-decision-required";

    /// <summary>Stable reason code emitted for <see cref="RestoredBackupAdmissionState.DeferredValidation"/>.</summary>
    public const string DeferredValidationCode = "deferred-validation";

    /// <summary>Stable reason code emitted for <see cref="RestoredBackupAdmissionState.Pending"/>.</summary>
    public const string PendingCode = "pending";

    /// <summary>Returns the canonical reason code for the supplied admission state.</summary>
    /// <param name="state">The admission state.</param>
    /// <returns>The stable kebab-case reason code.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown for unknown enum values.</exception>
    public static string ReasonCodeFor(RestoredBackupAdmissionState state) => state switch {
        RestoredBackupAdmissionState.Accepted => AcceptedCode,
        RestoredBackupAdmissionState.Blocked => BlockedCode,
        RestoredBackupAdmissionState.Quarantined => QuarantinedCode,
        RestoredBackupAdmissionState.OperatorDecisionRequired => OperatorDecisionRequiredCode,
        RestoredBackupAdmissionState.DeferredValidation => DeferredValidationCode,
        RestoredBackupAdmissionState.Pending => PendingCode,
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown RestoredBackupAdmissionState value."),
    };

    /// <summary>Returns the canonical operator next-action hint for the supplied admission state.</summary>
    /// <param name="state">The admission state.</param>
    /// <returns>The operator next-action hint.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown for unknown enum values.</exception>
    public static CryptoShreddingNextAction NextActionFor(RestoredBackupAdmissionState state) => state switch {
        RestoredBackupAdmissionState.Accepted => CryptoShreddingNextAction.None,
        RestoredBackupAdmissionState.Blocked => CryptoShreddingNextAction.None,
        RestoredBackupAdmissionState.Quarantined => CryptoShreddingNextAction.SubmitOperatorDecision,
        RestoredBackupAdmissionState.OperatorDecisionRequired => CryptoShreddingNextAction.SubmitOperatorDecision,
        RestoredBackupAdmissionState.DeferredValidation => CryptoShreddingNextAction.ProvideRestoreEvidence,
        RestoredBackupAdmissionState.Pending => CryptoShreddingNextAction.SubmitOperatorDecision,
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown RestoredBackupAdmissionState value."),
    };

    /// <summary>Projects this admission result into a <see cref="ProtectedDataReadabilityDecision"/>.</summary>
    /// <param name="stage">The pipeline stage that produced the decision.</param>
    /// <param name="metadataVersion">The metadata schema version observed for the affected row.</param>
    /// <param name="sequenceNumber">The exact affected sequence number, when known.</param>
    /// <returns>A readability decision aligned to the admission state.</returns>
    public ProtectedDataReadabilityDecision ToReadabilityDecision(
        ProtectedDataDecisionStage stage,
        int metadataVersion,
        long? sequenceNumber = null) => State switch {
            RestoredBackupAdmissionState.Accepted => ProtectedDataReadabilityDecision.Readable(
                stage, TenantId, Domain, AggregateId, sequenceNumber ?? FromSequence, metadataVersion, CorrelationId),
            RestoredBackupAdmissionState.Blocked => ProtectedDataReadabilityDecision.RestoreConflict(
                stage, TenantId, Domain, AggregateId, sequenceNumber ?? FromSequence, metadataVersion, CorrelationId, AuditId),
            RestoredBackupAdmissionState.Quarantined => ProtectedDataReadabilityDecision.QuarantineRequired(
                stage, TenantId, Domain, AggregateId, sequenceNumber ?? FromSequence, metadataVersion, CorrelationId, AuditId),
            RestoredBackupAdmissionState.OperatorDecisionRequired => ProtectedDataReadabilityDecision.OperatorDecisionRequired(
                stage, TenantId, Domain, AggregateId, sequenceNumber ?? FromSequence, metadataVersion, CorrelationId, AuditId),
            RestoredBackupAdmissionState.DeferredValidation => ProtectedDataReadabilityDecision.DeferredValidation(
                stage, TenantId, Domain, AggregateId, sequenceNumber ?? FromSequence, metadataVersion, CorrelationId, AuditId),
            RestoredBackupAdmissionState.Pending => ProtectedDataReadabilityDecision.OperatorDecisionRequired(
                stage, TenantId, Domain, AggregateId, sequenceNumber ?? FromSequence, metadataVersion, CorrelationId, AuditId),
            _ => throw new ArgumentOutOfRangeException(nameof(State), State, "Unknown RestoredBackupAdmissionState value."),
        };
}
