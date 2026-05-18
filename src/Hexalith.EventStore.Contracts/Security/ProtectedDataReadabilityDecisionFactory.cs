using System;

namespace Hexalith.EventStore.Contracts.Security;

/// <summary>
/// Story 22.7c — single EventStore-owned helper that maps an unprotection outcome plus optional
/// restore-admission context onto a <see cref="ProtectedDataReadabilityDecision"/>. Runtime call
/// sites (publisher, actor, snapshot manager, replay controller, admin inspection) consume this
/// helper instead of recomputing policy locally.
/// </summary>
public static class ProtectedDataReadabilityDecisionFactory {
    /// <summary>
    /// Builds a decision for a payload-unprotection outcome.
    /// </summary>
    /// <param name="outcome">The typed outcome returned by the provider.</param>
    /// <param name="stage">The pipeline stage that produced the decision.</param>
    /// <param name="tenantId">The tenant scope.</param>
    /// <param name="domain">The domain scope.</param>
    /// <param name="aggregateId">The aggregate identifier.</param>
    /// <param name="sequenceNumber">The affected sequence number.</param>
    /// <param name="correlationId">Optional correlation identifier.</param>
    /// <param name="restoreAdmission">Optional admission result whose conflict overrides the readability decision.</param>
    /// <returns>A readability decision.</returns>
    public static ProtectedDataReadabilityDecision FromOutcome(
        PayloadUnprotectionOutcome outcome,
        ProtectedDataDecisionStage stage,
        string tenantId,
        string domain,
        string? aggregateId,
        long? sequenceNumber,
        string? correlationId = null,
        RestoredBackupAdmissionResult? restoreAdmission = null) {
        ArgumentNullException.ThrowIfNull(outcome);
        int metadataVersion = outcome.Metadata.MetadataVersion;
        if (restoreAdmission is not null
            && restoreAdmission.State != RestoredBackupAdmissionState.Accepted
            && AdmissionMatches(restoreAdmission, tenantId, domain, aggregateId, sequenceNumber)) {
            return restoreAdmission.ToReadabilityDecision(stage, metadataVersion, sequenceNumber);
        }

        if (outcome.IsReadable) {
            return ProtectedDataReadabilityDecision.Readable(
                stage, tenantId, domain, aggregateId, sequenceNumber, metadataVersion, correlationId);
        }

        return ProtectedDataReadabilityDecision.FromUnreadable(
            outcome.UnreadableReason!.Value,
            stage,
            tenantId,
            domain,
            aggregateId,
            sequenceNumber,
            metadataVersion,
            correlationId);
    }

    /// <summary>
    /// Builds a decision for a snapshot-unprotection outcome.
    /// </summary>
    /// <param name="outcome">The typed outcome returned by the provider.</param>
    /// <param name="stage">The pipeline stage that produced the decision.</param>
    /// <param name="tenantId">The tenant scope.</param>
    /// <param name="domain">The domain scope.</param>
    /// <param name="aggregateId">The aggregate identifier.</param>
    /// <param name="sequenceNumber">The affected sequence number.</param>
    /// <param name="correlationId">Optional correlation identifier.</param>
    /// <param name="restoreAdmission">Optional admission result whose conflict overrides the readability decision.</param>
    /// <returns>A readability decision.</returns>
    public static ProtectedDataReadabilityDecision FromOutcome(
        SnapshotUnprotectionOutcome outcome,
        ProtectedDataDecisionStage stage,
        string tenantId,
        string domain,
        string? aggregateId,
        long? sequenceNumber,
        string? correlationId = null,
        RestoredBackupAdmissionResult? restoreAdmission = null) {
        ArgumentNullException.ThrowIfNull(outcome);
        int metadataVersion = outcome.Metadata.MetadataVersion;
        if (restoreAdmission is not null
            && restoreAdmission.State != RestoredBackupAdmissionState.Accepted
            && AdmissionMatches(restoreAdmission, tenantId, domain, aggregateId, sequenceNumber)) {
            return restoreAdmission.ToReadabilityDecision(stage, metadataVersion, sequenceNumber);
        }

        if (outcome.IsReadable) {
            return ProtectedDataReadabilityDecision.Readable(
                stage, tenantId, domain, aggregateId, sequenceNumber, metadataVersion, correlationId);
        }

        return ProtectedDataReadabilityDecision.FromUnreadable(
            outcome.UnreadableReason!.Value,
            stage,
            tenantId,
            domain,
            aggregateId,
            sequenceNumber,
            metadataVersion,
            correlationId);
    }

    /// <summary>
    /// Builds a decision directly from stored protection metadata (used by surfaces that inspect
    /// metadata before calling the provider, e.g. the public stream-read endpoint).
    /// </summary>
    /// <param name="metadata">The metadata observed on the stored envelope.</param>
    /// <param name="stage">The pipeline stage that produced the decision.</param>
    /// <param name="tenantId">The tenant scope.</param>
    /// <param name="domain">The domain scope.</param>
    /// <param name="aggregateId">The aggregate identifier.</param>
    /// <param name="sequenceNumber">The affected sequence number.</param>
    /// <param name="correlationId">Optional correlation identifier.</param>
    /// <param name="restoreAdmission">Optional admission result whose conflict overrides the readability decision.</param>
    /// <returns>A readability decision.</returns>
    public static ProtectedDataReadabilityDecision FromMetadata(
        EventStorePayloadProtectionMetadata metadata,
        ProtectedDataDecisionStage stage,
        string tenantId,
        string domain,
        string? aggregateId,
        long? sequenceNumber,
        string? correlationId = null,
        RestoredBackupAdmissionResult? restoreAdmission = null) {
        ArgumentNullException.ThrowIfNull(metadata);
        int metadataVersion = metadata.MetadataVersion;
        if (restoreAdmission is not null
            && restoreAdmission.State != RestoredBackupAdmissionState.Accepted
            && AdmissionMatches(restoreAdmission, tenantId, domain, aggregateId, sequenceNumber)) {
            return restoreAdmission.ToReadabilityDecision(stage, metadataVersion, sequenceNumber);
        }

        return metadata.State switch {
            PayloadProtectionState.Unprotected =>
                ProtectedDataReadabilityDecision.Readable(stage, tenantId, domain, aggregateId, sequenceNumber, metadataVersion, correlationId),
            PayloadProtectionState.Protected =>
                ProtectedDataReadabilityDecision.DeferredValidation(stage, tenantId, domain, aggregateId, sequenceNumber, metadataVersion, correlationId),
            PayloadProtectionState.ProviderOpaque =>
                ProtectedDataReadabilityDecision.FromUnreadable(
                    UnreadableProtectedDataReason.ProviderOpaqueUnsupportedOperation,
                    stage,
                    tenantId,
                    domain,
                    aggregateId,
                    sequenceNumber,
                    metadataVersion,
                    correlationId),
            _ => throw new ArgumentOutOfRangeException(nameof(metadata), metadata.State, "Unknown PayloadProtectionState value."),
        };
    }

    private static bool AdmissionMatches(
        RestoredBackupAdmissionResult admission,
        string tenantId,
        string domain,
        string? aggregateId,
        long? sequenceNumber) {
        if (!string.Equals(admission.TenantId, tenantId, StringComparison.Ordinal)
            || !string.Equals(admission.Domain, domain, StringComparison.Ordinal)) {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(admission.AggregateId)
            && !string.Equals(admission.AggregateId, aggregateId, StringComparison.Ordinal)) {
            return false;
        }

        if (!sequenceNumber.HasValue) {
            return true;
        }

        if (admission.FromSequence.HasValue && sequenceNumber.Value < admission.FromSequence.Value) {
            return false;
        }

        return !admission.ToSequence.HasValue || sequenceNumber.Value <= admission.ToSequence.Value;
    }
}
