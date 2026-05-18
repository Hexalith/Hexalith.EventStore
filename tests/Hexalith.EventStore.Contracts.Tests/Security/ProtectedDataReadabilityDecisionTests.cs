using System;
using System.Collections.Generic;

using Hexalith.EventStore.Contracts.Security;

using Shouldly;

namespace Hexalith.EventStore.Contracts.Tests.Security;

/// <summary>
/// Story 22.7c — contract tests for the canonical readability decision shape and the factory that
/// produces it from 22.7b outcomes / 22.7a metadata.
/// </summary>
public class ProtectedDataReadabilityDecisionTests {
    [Fact]
    public void Readable_BuildsExpectedShape() {
        ProtectedDataReadabilityDecision decision = ProtectedDataReadabilityDecision.Readable(
            ProtectedDataDecisionStage.Replay,
            "t1",
            "orders",
            "agg-1",
            42,
            metadataVersion: 1);

        decision.Status.ShouldBe(ProtectedDataReadabilityStatus.Readable);
        decision.IsReadable.ShouldBeTrue();
        decision.IsPermanent.ShouldBeFalse();
        decision.IsRetryable.ShouldBeFalse();
        decision.UnreadableReason.ShouldBeNull();
        decision.ReasonCode.ShouldBe(ProtectedDataReadabilityDecision.ReadableCode);
        decision.NextAction.ShouldBe(CryptoShreddingNextAction.None);
    }

    public static IEnumerable<object[]> AllReasons() {
        foreach (UnreadableProtectedDataReason reason in Enum.GetValues<UnreadableProtectedDataReason>()) {
            yield return new object[] { reason };
        }
    }

    [Theory]
    [MemberData(nameof(AllReasons))]
    public void FromUnreadable_MapsTo22_7bReasonCode(UnreadableProtectedDataReason reason) {
        ProtectedDataReadabilityDecision decision = ProtectedDataReadabilityDecision.FromUnreadable(
            reason,
            ProtectedDataDecisionStage.Rehydrate,
            "t1",
            "orders",
            "agg-1",
            42,
            metadataVersion: 1);

        decision.IsReadable.ShouldBeFalse();
        decision.UnreadableReason.ShouldBe(reason);
        decision.ReasonCode.ShouldBe(UnreadableProtectedDataReasonCodes.From(reason));
        decision.IsRetryable.ShouldBe(UnreadableProtectedDataReasonCodes.IsRetryable(reason));
        decision.IsPermanent.ShouldBe(UnreadableProtectedDataReasonCodes.IsPermanent(reason));
    }

    [Fact]
    public void RestoreConflict_IsPermanent_AndAsksForOperatorDecision() {
        ProtectedDataReadabilityDecision decision = ProtectedDataReadabilityDecision.RestoreConflict(
            ProtectedDataDecisionStage.BackupAdmission,
            "t1",
            "orders",
            aggregateId: "agg-1",
            sequenceNumber: 42,
            metadataVersion: 1);

        decision.Status.ShouldBe(ProtectedDataReadabilityStatus.RestoreConflict);
        decision.IsReadable.ShouldBeFalse();
        decision.IsPermanent.ShouldBeTrue();
        decision.IsRetryable.ShouldBeFalse();
        decision.NextAction.ShouldBe(CryptoShreddingNextAction.SubmitOperatorDecision);
        decision.ReasonCode.ShouldBe(ProtectedDataReadabilityDecision.RestoreConflictCode);
    }

    [Fact]
    public void DeferredValidation_IsRetryable_AndAsksForRestoreEvidence() {
        ProtectedDataReadabilityDecision decision = ProtectedDataReadabilityDecision.DeferredValidation(
            ProtectedDataDecisionStage.BackupAdmission,
            "t1",
            "orders",
            aggregateId: null,
            sequenceNumber: null,
            metadataVersion: 1);

        decision.Status.ShouldBe(ProtectedDataReadabilityStatus.DeferredValidation);
        decision.IsReadable.ShouldBeFalse();
        decision.IsRetryable.ShouldBeTrue();
        decision.IsPermanent.ShouldBeFalse();
        decision.NextAction.ShouldBe(CryptoShreddingNextAction.ProvideRestoreEvidence);
        decision.ReasonCode.ShouldBe(ProtectedDataReadabilityDecision.DeferredValidationCode);
    }

    [Fact]
    public void Factory_FromOutcome_RestoreAdmissionConflict_OverridesReadable() {
        // Readable provider outcome but admission says Blocked → decision must report RestoreConflict.
        PayloadUnprotectionOutcome readable = PayloadUnprotectionOutcome.Readable(
            new byte[] { 1, 2 },
            "application/json",
            EventStorePayloadProtectionMetadata.Unprotected());
        RestoredBackupAdmissionResult blocked = new(
            AdmissionId: "01HKAD",
            State: RestoredBackupAdmissionState.Blocked,
            TenantId: "t1",
            Domain: "orders",
            AggregateId: "agg-1",
            FromSequence: 10,
            ToSequence: 20,
            BackupManifestId: "manifest-1",
            ProtectionMetadataVersion: 1,
            KeyReferencePolicy: KeyReferencePolicy.NoKeyReference,
            KeyAliasFingerprint: null,
            WatermarkConflict: "backup-before-deletion",
            ReasonCode: RestoredBackupAdmissionResult.BlockedCode,
            NextAction: CryptoShreddingNextAction.None,
            CorrelationId: null,
            AuditId: "01HKAU",
            DecisionActorId: "operator",
            DecidedAtUtc: DateTimeOffset.UtcNow,
            IdempotentReplay: false);

        ProtectedDataReadabilityDecision decision = ProtectedDataReadabilityDecisionFactory.FromOutcome(
            readable,
            ProtectedDataDecisionStage.Replay,
            "t1",
            "orders",
            "agg-1",
            15,
            restoreAdmission: blocked);

        decision.IsReadable.ShouldBeFalse();
        decision.Status.ShouldBe(ProtectedDataReadabilityStatus.RestoreConflict);
        decision.AuditId.ShouldBe("01HKAU");
    }

    [Fact]
    public void Factory_FromOutcome_IgnoresAdmissionForDifferentScope() {
        PayloadUnprotectionOutcome readable = PayloadUnprotectionOutcome.Readable(
            new byte[] { 1, 2 },
            "application/json",
            EventStorePayloadProtectionMetadata.Unprotected());
        RestoredBackupAdmissionResult blocked = new(
            AdmissionId: "01HKAD",
            State: RestoredBackupAdmissionState.Blocked,
            TenantId: "other-tenant",
            Domain: "orders",
            AggregateId: "agg-1",
            FromSequence: 10,
            ToSequence: 20,
            BackupManifestId: "manifest-1",
            ProtectionMetadataVersion: 1,
            KeyReferencePolicy: KeyReferencePolicy.NoKeyReference,
            KeyAliasFingerprint: null,
            WatermarkConflict: "backup-before-deletion",
            ReasonCode: RestoredBackupAdmissionResult.BlockedCode,
            NextAction: CryptoShreddingNextAction.None,
            CorrelationId: null,
            AuditId: "01HKAU",
            DecisionActorId: "operator",
            DecidedAtUtc: DateTimeOffset.UtcNow,
            IdempotentReplay: false);

        ProtectedDataReadabilityDecision decision = ProtectedDataReadabilityDecisionFactory.FromOutcome(
            readable,
            ProtectedDataDecisionStage.Replay,
            "t1",
            "orders",
            "agg-1",
            42,
            restoreAdmission: blocked);

        decision.IsReadable.ShouldBeTrue();
    }

    [Fact]
    public void Factory_FromMetadata_ProviderOpaque_MapsToUnreadable() {
        EventStorePayloadProtectionMetadata opaque = EventStorePayloadProtectionMetadata.ProviderOpaque("forbidden");

        ProtectedDataReadabilityDecision decision = ProtectedDataReadabilityDecisionFactory.FromMetadata(
            opaque,
            ProtectedDataDecisionStage.Replay,
            "t1",
            "orders",
            "agg-1",
            42);

        decision.IsReadable.ShouldBeFalse();
        decision.UnreadableReason.ShouldBe(UnreadableProtectedDataReason.ProviderOpaqueUnsupportedOperation);
        decision.Status.ShouldBe(ProtectedDataReadabilityStatus.ProviderOpaque);
    }

    [Fact]
    public void Factory_FromMetadata_ProtectedWithoutProviderOutcome_DefersValidation() {
        EventStorePayloadProtectionMetadata protectedMetadata = EventStorePayloadProtectionMetadata.Unprotected(PayloadProtectionState.Protected);

        ProtectedDataReadabilityDecision decision = ProtectedDataReadabilityDecisionFactory.FromMetadata(
            protectedMetadata,
            ProtectedDataDecisionStage.Replay,
            "t1",
            "orders",
            "agg-1",
            42);

        decision.IsReadable.ShouldBeFalse();
        decision.Status.ShouldBe(ProtectedDataReadabilityStatus.DeferredValidation);
        decision.NextAction.ShouldBe(CryptoShreddingNextAction.ProvideRestoreEvidence);
    }
}
