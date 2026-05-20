using System.Text.Json;

using Hexalith.EventStore.Contracts.Problems;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.Contracts.Tests.Security;

/// <summary>
/// Story 22.7c — contract tests for the restored-backup admission state machine, request
/// validation, problem details mapping, and JSON serialization.
/// </summary>
public class RestoredBackupAdmissionTests {
    private static RestoredBackupAdmissionRequest ValidRequest()
        => new(
            AdmissionId: "01HKAD",
            TenantId: "t1",
            Domain: "orders",
            AggregateId: "agg-1",
            FromSequence: 10,
            ToSequence: 20,
            BackupManifestId: "manifest-1",
            BackupCreatedAtUtc: DateTimeOffset.UtcNow,
            RestoreRequestedAtUtc: DateTimeOffset.UtcNow,
            ProtectionMetadataVersion: 1,
            KeyReferencePolicy: KeyReferencePolicy.NoKeyReference,
            KeyAliasFingerprint: null,
            DeletionWatermarkUtc: null,
            CorrelationId: "01HKCORR",
            OperatorActorId: "operator");

    [Fact]
    public void Request_TryValidate_AcceptsValidShape() => ValidRequest().TryValidate(out string? reason).ShouldBeTrue(reason ?? "should be valid");

    [Fact]
    public void Request_TryValidate_RejectsInvertedRange() {
        RestoredBackupAdmissionRequest bad = ValidRequest() with { FromSequence = 20, ToSequence = 10 };

        bad.TryValidate(out string? reason).ShouldBeFalse();
        reason.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Request_TryValidate_RejectsFingerprintWhenPolicyForbidsIt() {
        RestoredBackupAdmissionRequest bad = ValidRequest() with {
            KeyReferencePolicy = KeyReferencePolicy.NoKeyReference,
            KeyAliasFingerprint = "deadbeef12345678",
        };

        bad.TryValidate(out string? reason).ShouldBeFalse();
        reason.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Request_TryValidate_RejectsFingerprintShape() {
        RestoredBackupAdmissionRequest bad = ValidRequest() with {
            KeyReferencePolicy = KeyReferencePolicy.AliasOnly,
            KeyAliasFingerprint = "zzzzzzzzzzzzzzzz",
        };

        bad.TryValidate(out string? reason).ShouldBeFalse();
        reason.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Request_TryValidate_RejectsNegativeToSequence_WhenFromSequenceMissing() {
        RestoredBackupAdmissionRequest bad = ValidRequest() with {
            FromSequence = null,
            ToSequence = -1,
        };

        bad.TryValidate(out string? reason).ShouldBeFalse();
        reason.ShouldNotBeNullOrWhiteSpace();
    }

    public static IEnumerable<object[]> AllStates() {
        foreach (RestoredBackupAdmissionState state in Enum.GetValues<RestoredBackupAdmissionState>()) {
            yield return new object[] { state };
        }
    }

    [Theory]
    [MemberData(nameof(AllStates))]
    public void ReasonCodeFor_IsStableAndKebab(RestoredBackupAdmissionState state) {
        string code = RestoredBackupAdmissionResult.ReasonCodeFor(state);

        code.ShouldNotBeNullOrWhiteSpace();
        code.ShouldBe(code.ToLowerInvariant());
    }

    [Fact]
    public void ReasonCodes_AllUnique() {
        var codes = new HashSet<string>(StringComparer.Ordinal);
        foreach (RestoredBackupAdmissionState state in Enum.GetValues<RestoredBackupAdmissionState>()) {
            codes.Add(RestoredBackupAdmissionResult.ReasonCodeFor(state)).ShouldBeTrue($"Duplicate code for {state}.");
        }
    }

    [Fact]
    public void Transitions_TerminalStates_AreTerminal() {
        RestoredBackupAdmissionTransitions.IsTerminal(RestoredBackupAdmissionState.Accepted).ShouldBeTrue();
        RestoredBackupAdmissionTransitions.IsTerminal(RestoredBackupAdmissionState.Blocked).ShouldBeTrue();
    }

    [Fact]
    public void Transitions_PendingAllowsAllNonInitial() {
        RestoredBackupAdmissionTransitions.IsAllowed(
            RestoredBackupAdmissionState.Pending,
            RestoredBackupAdmissionState.Accepted).ShouldBeTrue();
        RestoredBackupAdmissionTransitions.IsAllowed(
            RestoredBackupAdmissionState.Pending,
            RestoredBackupAdmissionState.Blocked).ShouldBeTrue();
        RestoredBackupAdmissionTransitions.IsAllowed(
            RestoredBackupAdmissionState.Pending,
            RestoredBackupAdmissionState.DeferredValidation).ShouldBeTrue();
    }

    [Fact]
    public void Transitions_DeferredValidation_AllowsAcceptanceAfterEvidence() => RestoredBackupAdmissionTransitions.IsAllowed(
            RestoredBackupAdmissionState.DeferredValidation,
            RestoredBackupAdmissionState.Accepted).ShouldBeTrue();

    [Fact]
    public void Transitions_TerminalStatesCannotMove() {
        foreach (RestoredBackupAdmissionState target in Enum.GetValues<RestoredBackupAdmissionState>()) {
            RestoredBackupAdmissionTransitions.IsAllowed(RestoredBackupAdmissionState.Accepted, target).ShouldBeFalse();
            RestoredBackupAdmissionTransitions.IsAllowed(RestoredBackupAdmissionState.Blocked, target).ShouldBeFalse();
        }
    }

    [Theory]
    [MemberData(nameof(AllStates))]
    public void Problem_GetStatusCode_IsHttpRange(RestoredBackupAdmissionState state) {
        int code = RestoredBackupAdmissionProblem.GetStatusCode(state);

        code.ShouldBeInRange(200, 599);
    }

    [Fact]
    public void Result_ToReadabilityDecision_BlockedProducesRestoreConflict() {
        var result = new RestoredBackupAdmissionResult(
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

        ProtectedDataReadabilityDecision decision = result.ToReadabilityDecision(ProtectedDataDecisionStage.Replay, 1);

        decision.Status.ShouldBe(ProtectedDataReadabilityStatus.RestoreConflict);
        decision.IsReadable.ShouldBeFalse();
    }

    [Fact]
    public void Result_ToReadabilityDecision_UsesExactSequenceWhenSupplied() {
        var result = new RestoredBackupAdmissionResult(
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

        ProtectedDataReadabilityDecision decision = result.ToReadabilityDecision(ProtectedDataDecisionStage.Replay, 1, 15);

        decision.SequenceNumber.ShouldBe(15);
    }

    [Fact]
    public void Result_NextActionFor_UnknownState_Throws() => _ = Should.Throw<ArgumentOutOfRangeException>(
            () => RestoredBackupAdmissionResult.NextActionFor((RestoredBackupAdmissionState)999));

    [Fact]
    public void Result_JsonRoundTrip_PreservesContract() {
        var result = new RestoredBackupAdmissionResult(
            AdmissionId: "01HKAD",
            State: RestoredBackupAdmissionState.DeferredValidation,
            TenantId: "t1",
            Domain: "orders",
            AggregateId: "agg-1",
            FromSequence: 10,
            ToSequence: 20,
            BackupManifestId: "manifest-1",
            ProtectionMetadataVersion: 1,
            KeyReferencePolicy: KeyReferencePolicy.NoKeyReference,
            KeyAliasFingerprint: null,
            WatermarkConflict: "backup-engine-deferred",
            ReasonCode: RestoredBackupAdmissionResult.DeferredValidationCode,
            NextAction: CryptoShreddingNextAction.ProvideRestoreEvidence,
            CorrelationId: "01HKCORR",
            AuditId: null,
            DecisionActorId: "operator",
            DecidedAtUtc: DateTimeOffset.Parse("2026-05-18T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
            IdempotentReplay: false);

        string json = JsonSerializer.Serialize(result);
        RestoredBackupAdmissionResult? round = JsonSerializer.Deserialize<RestoredBackupAdmissionResult>(json);

        _ = round.ShouldNotBeNull();
        round.ShouldBe(result);
    }
}
