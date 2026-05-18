using System;

namespace Hexalith.EventStore.Contracts.Security;

/// <summary>
/// Story 22.7c — single EventStore-owned readability decision result. Every read, publish, replay,
/// rebuild, snapshot-load, backup-admission, admin, CLI, and MCP surface routes protected-data
/// decisions through this record so policy is computed once and surfaced consistently.
/// </summary>
/// <param name="Status">The canonical decision status.</param>
/// <param name="UnreadableReason">When the decision is unreadable, the precise 22.7b reason category; otherwise <see langword="null"/>.</param>
/// <param name="Stage">The pipeline stage that produced the decision.</param>
/// <param name="TenantId">The tenant scope of the affected data.</param>
/// <param name="Domain">The domain scope of the affected data.</param>
/// <param name="AggregateId">The affected aggregate identifier (optional for tenant-scoped decisions).</param>
/// <param name="SequenceNumber">The affected sequence number when relevant to a single event/snapshot.</param>
/// <param name="MetadataVersion">The protection metadata schema version observed.</param>
/// <param name="CorrelationId">Optional correlation identifier (ULID, request id, or audit id).</param>
/// <param name="AuditId">Optional audit identifier referencing a stored <see cref="CryptoShreddingAuditEvent"/>.</param>
/// <param name="NextAction">The operator-facing next action hint.</param>
/// <param name="ReasonCode">The stable kebab-case reason code (mirrors the 22.7b code when applicable).</param>
public sealed record ProtectedDataReadabilityDecision(
    ProtectedDataReadabilityStatus Status,
    UnreadableProtectedDataReason? UnreadableReason,
    ProtectedDataDecisionStage Stage,
    string TenantId,
    string Domain,
    string? AggregateId,
    long? SequenceNumber,
    int MetadataVersion,
    string? CorrelationId,
    string? AuditId,
    CryptoShreddingNextAction NextAction,
    string ReasonCode) {
    /// <summary>Stable reason code emitted when the decision is <see cref="ProtectedDataReadabilityStatus.Readable"/>.</summary>
    public const string ReadableCode = "readable";

    /// <summary>Stable reason code emitted when the decision is <see cref="ProtectedDataReadabilityStatus.DeferredValidation"/>.</summary>
    public const string DeferredValidationCode = "deferred-validation";

    /// <summary>Stable reason code emitted when the decision is <see cref="ProtectedDataReadabilityStatus.RestoreConflict"/>.</summary>
    public const string RestoreConflictCode = "restore-conflict";

    /// <summary>Stable reason code emitted when the decision is <see cref="ProtectedDataReadabilityStatus.QuarantineRequired"/>.</summary>
    public const string QuarantineRequiredCode = "quarantine-required";

    /// <summary>Stable reason code emitted when the decision is <see cref="ProtectedDataReadabilityStatus.OperatorDecisionRequired"/>.</summary>
    public const string OperatorDecisionRequiredCode = "operator-decision-required";

    /// <summary>Returns <see langword="true"/> when the underlying data may be served.</summary>
    public bool IsReadable => Status == ProtectedDataReadabilityStatus.Readable;

    /// <summary>Returns <see langword="true"/> when a retry may succeed after backoff.</summary>
    public bool IsRetryable => Status is
        ProtectedDataReadabilityStatus.ProviderUnavailable
        or ProtectedDataReadabilityStatus.DeferredValidation;

    /// <summary>Returns <see langword="true"/> when the condition is permanent and cannot resolve without operator action.</summary>
    public bool IsPermanent => Status is
        ProtectedDataReadabilityStatus.MalformedMetadata
        or ProtectedDataReadabilityStatus.ProviderOpaque
        or ProtectedDataReadabilityStatus.RestoreConflict
        or ProtectedDataReadabilityStatus.QuarantineRequired
        || (UnreadableReason.HasValue && UnreadableProtectedDataReasonCodes.IsPermanent(UnreadableReason.Value));

    /// <summary>
    /// Constructs a readable decision for the supplied stage and identity.
    /// </summary>
    /// <param name="stage">The pipeline stage that produced the decision.</param>
    /// <param name="tenantId">The tenant scope.</param>
    /// <param name="domain">The domain scope.</param>
    /// <param name="aggregateId">The aggregate identifier (optional).</param>
    /// <param name="sequenceNumber">The affected sequence number (optional).</param>
    /// <param name="metadataVersion">The metadata schema version observed.</param>
    /// <param name="correlationId">Optional correlation identifier.</param>
    /// <returns>A readable decision.</returns>
    public static ProtectedDataReadabilityDecision Readable(
        ProtectedDataDecisionStage stage,
        string tenantId,
        string domain,
        string? aggregateId,
        long? sequenceNumber,
        int metadataVersion,
        string? correlationId = null) {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        return new ProtectedDataReadabilityDecision(
            Status: ProtectedDataReadabilityStatus.Readable,
            UnreadableReason: null,
            Stage: stage,
            TenantId: tenantId,
            Domain: domain,
            AggregateId: aggregateId,
            SequenceNumber: sequenceNumber,
            MetadataVersion: metadataVersion,
            CorrelationId: correlationId,
            AuditId: null,
            NextAction: CryptoShreddingNextAction.None,
            ReasonCode: ReadableCode);
    }

    /// <summary>
    /// Constructs an unreadable decision by mapping a 22.7b reason category onto a 22.7c status.
    /// </summary>
    /// <param name="reason">The 22.7b reason category.</param>
    /// <param name="stage">The pipeline stage that produced the decision.</param>
    /// <param name="tenantId">The tenant scope.</param>
    /// <param name="domain">The domain scope.</param>
    /// <param name="aggregateId">The aggregate identifier (optional).</param>
    /// <param name="sequenceNumber">The affected sequence number (optional).</param>
    /// <param name="metadataVersion">The metadata schema version observed.</param>
    /// <param name="correlationId">Optional correlation identifier.</param>
    /// <param name="auditId">Optional audit identifier.</param>
    /// <returns>An unreadable decision.</returns>
    public static ProtectedDataReadabilityDecision FromUnreadable(
        UnreadableProtectedDataReason reason,
        ProtectedDataDecisionStage stage,
        string tenantId,
        string domain,
        string? aggregateId,
        long? sequenceNumber,
        int metadataVersion,
        string? correlationId = null,
        string? auditId = null) {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        ProtectedDataReadabilityStatus status = MapStatus(reason);
        CryptoShreddingNextAction nextAction = MapNextAction(reason);
        string reasonCode = UnreadableProtectedDataReasonCodes.From(reason);
        return new ProtectedDataReadabilityDecision(
            Status: status,
            UnreadableReason: reason,
            Stage: stage,
            TenantId: tenantId,
            Domain: domain,
            AggregateId: aggregateId,
            SequenceNumber: sequenceNumber,
            MetadataVersion: metadataVersion,
            CorrelationId: correlationId,
            AuditId: auditId,
            NextAction: nextAction,
            ReasonCode: reasonCode);
    }

    /// <summary>
    /// Constructs a restore-conflict decision (irreversible workflow watermark conflicts with the
    /// restored data).
    /// </summary>
    /// <param name="stage">The pipeline stage that produced the decision.</param>
    /// <param name="tenantId">The tenant scope.</param>
    /// <param name="domain">The domain scope.</param>
    /// <param name="aggregateId">The aggregate identifier (optional).</param>
    /// <param name="sequenceNumber">The affected sequence number (optional).</param>
    /// <param name="metadataVersion">The metadata schema version observed.</param>
    /// <param name="correlationId">Optional correlation identifier.</param>
    /// <param name="auditId">Optional audit identifier referencing the conflict record.</param>
    /// <returns>A restore-conflict decision.</returns>
    public static ProtectedDataReadabilityDecision RestoreConflict(
        ProtectedDataDecisionStage stage,
        string tenantId,
        string domain,
        string? aggregateId,
        long? sequenceNumber,
        int metadataVersion,
        string? correlationId = null,
        string? auditId = null) {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        return new ProtectedDataReadabilityDecision(
            Status: ProtectedDataReadabilityStatus.RestoreConflict,
            UnreadableReason: UnreadableProtectedDataReason.ConsistencyMismatch,
            Stage: stage,
            TenantId: tenantId,
            Domain: domain,
            AggregateId: aggregateId,
            SequenceNumber: sequenceNumber,
            MetadataVersion: metadataVersion,
            CorrelationId: correlationId,
            AuditId: auditId,
            NextAction: CryptoShreddingNextAction.SubmitOperatorDecision,
            ReasonCode: RestoreConflictCode);
    }

    /// <summary>
    /// Constructs a deferred-validation decision (restore admission cannot prove safety yet).
    /// </summary>
    /// <param name="stage">The pipeline stage that produced the decision.</param>
    /// <param name="tenantId">The tenant scope.</param>
    /// <param name="domain">The domain scope.</param>
    /// <param name="aggregateId">The aggregate identifier (optional).</param>
    /// <param name="sequenceNumber">The affected sequence number (optional).</param>
    /// <param name="metadataVersion">The metadata schema version observed.</param>
    /// <param name="correlationId">Optional correlation identifier.</param>
    /// <param name="auditId">Optional audit identifier.</param>
    /// <returns>A deferred-validation decision.</returns>
    public static ProtectedDataReadabilityDecision DeferredValidation(
        ProtectedDataDecisionStage stage,
        string tenantId,
        string domain,
        string? aggregateId,
        long? sequenceNumber,
        int metadataVersion,
        string? correlationId = null,
        string? auditId = null) {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        return new ProtectedDataReadabilityDecision(
            Status: ProtectedDataReadabilityStatus.DeferredValidation,
            UnreadableReason: UnreadableProtectedDataReason.ProviderUnavailable,
            Stage: stage,
            TenantId: tenantId,
            Domain: domain,
            AggregateId: aggregateId,
            SequenceNumber: sequenceNumber,
            MetadataVersion: metadataVersion,
            CorrelationId: correlationId,
            AuditId: auditId,
            NextAction: CryptoShreddingNextAction.ProvideRestoreEvidence,
            ReasonCode: DeferredValidationCode);
    }

    /// <summary>
    /// Constructs a quarantine-required decision.
    /// </summary>
    /// <param name="stage">The pipeline stage that produced the decision.</param>
    /// <param name="tenantId">The tenant scope.</param>
    /// <param name="domain">The domain scope.</param>
    /// <param name="aggregateId">The aggregate identifier (optional).</param>
    /// <param name="sequenceNumber">The affected sequence number (optional).</param>
    /// <param name="metadataVersion">The metadata schema version observed.</param>
    /// <param name="correlationId">Optional correlation identifier.</param>
    /// <param name="auditId">Optional audit identifier referencing the quarantine record.</param>
    /// <returns>A quarantine-required decision.</returns>
    public static ProtectedDataReadabilityDecision QuarantineRequired(
        ProtectedDataDecisionStage stage,
        string tenantId,
        string domain,
        string? aggregateId,
        long? sequenceNumber,
        int metadataVersion,
        string? correlationId = null,
        string? auditId = null) {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        return new ProtectedDataReadabilityDecision(
            Status: ProtectedDataReadabilityStatus.QuarantineRequired,
            UnreadableReason: UnreadableProtectedDataReason.ConsistencyMismatch,
            Stage: stage,
            TenantId: tenantId,
            Domain: domain,
            AggregateId: aggregateId,
            SequenceNumber: sequenceNumber,
            MetadataVersion: metadataVersion,
            CorrelationId: correlationId,
            AuditId: auditId,
            NextAction: CryptoShreddingNextAction.SubmitOperatorDecision,
            ReasonCode: QuarantineRequiredCode);
    }

    /// <summary>
    /// Constructs an operator-decision-required decision.
    /// </summary>
    /// <param name="stage">The pipeline stage that produced the decision.</param>
    /// <param name="tenantId">The tenant scope.</param>
    /// <param name="domain">The domain scope.</param>
    /// <param name="aggregateId">The aggregate identifier (optional).</param>
    /// <param name="sequenceNumber">The affected sequence number (optional).</param>
    /// <param name="metadataVersion">The metadata schema version observed.</param>
    /// <param name="correlationId">Optional correlation identifier.</param>
    /// <param name="auditId">Optional audit identifier referencing the conflict record.</param>
    /// <returns>An operator-decision-required decision.</returns>
    public static ProtectedDataReadabilityDecision OperatorDecisionRequired(
        ProtectedDataDecisionStage stage,
        string tenantId,
        string domain,
        string? aggregateId,
        long? sequenceNumber,
        int metadataVersion,
        string? correlationId = null,
        string? auditId = null) {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        return new ProtectedDataReadabilityDecision(
            Status: ProtectedDataReadabilityStatus.OperatorDecisionRequired,
            UnreadableReason: UnreadableProtectedDataReason.ConsistencyMismatch,
            Stage: stage,
            TenantId: tenantId,
            Domain: domain,
            AggregateId: aggregateId,
            SequenceNumber: sequenceNumber,
            MetadataVersion: metadataVersion,
            CorrelationId: correlationId,
            AuditId: auditId,
            NextAction: CryptoShreddingNextAction.SubmitOperatorDecision,
            ReasonCode: OperatorDecisionRequiredCode);
    }

    private static ProtectedDataReadabilityStatus MapStatus(UnreadableProtectedDataReason reason) => reason switch {
        UnreadableProtectedDataReason.MalformedMetadata => ProtectedDataReadabilityStatus.MalformedMetadata,
        UnreadableProtectedDataReason.UnknownMetadataVersion => ProtectedDataReadabilityStatus.UnknownVersion,
        UnreadableProtectedDataReason.ProviderOpaqueUnsupportedOperation => ProtectedDataReadabilityStatus.ProviderOpaque,
        UnreadableProtectedDataReason.ProviderUnavailable => ProtectedDataReadabilityStatus.ProviderUnavailable,
        _ => ProtectedDataReadabilityStatus.Unreadable,
    };

    private static CryptoShreddingNextAction MapNextAction(UnreadableProtectedDataReason reason) => reason switch {
        UnreadableProtectedDataReason.MissingKey => CryptoShreddingNextAction.RegisterKey,
        UnreadableProtectedDataReason.KeyInvalidatedOrDeleted => CryptoShreddingNextAction.None,
        UnreadableProtectedDataReason.ProviderUnavailable => CryptoShreddingNextAction.RetryWithBackoff,
        UnreadableProtectedDataReason.ProviderDenied => CryptoShreddingNextAction.SubmitOperatorDecision,
        UnreadableProtectedDataReason.ConsistencyMismatch => CryptoShreddingNextAction.InvestigateProvenance,
        UnreadableProtectedDataReason.MalformedMetadata => CryptoShreddingNextAction.InvestigateProvenance,
        UnreadableProtectedDataReason.UnknownMetadataVersion => CryptoShreddingNextAction.UpgradeEventStore,
        UnreadableProtectedDataReason.ProviderOpaqueUnsupportedOperation => CryptoShreddingNextAction.SubmitOperatorDecision,
        UnreadableProtectedDataReason.BytesMetadataMismatch => CryptoShreddingNextAction.InvestigateProvenance,
        _ => CryptoShreddingNextAction.None,
    };
}
