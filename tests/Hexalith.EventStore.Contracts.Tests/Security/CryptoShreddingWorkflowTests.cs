using System;
using System.Collections.Generic;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Security;

using Shouldly;

namespace Hexalith.EventStore.Contracts.Tests.Security;

/// <summary>
/// Story 22.7c — contract tests for the crypto-shredding workflow state machine, identity
/// idempotency, reason codes, and audit record validation.
/// </summary>
public class CryptoShreddingWorkflowTests {
    [Fact]
    public void Identity_Equality_IsValueBased() {
        var a = new CryptoShreddingWorkflowIdentity(
            WorkflowId: "01HKAAAAAAAAAAAAAAAAAAAAAA",
            TenantId: "t1",
            Domain: "orders",
            Scope: CryptoShreddingWorkflowScope.Aggregate,
            AggregateId: "agg-1",
            FromSequence: null,
            ToSequence: null,
            KeyReferencePolicy: KeyReferencePolicy.NoKeyReference,
            KeyAliasFingerprint: null);
        var b = new CryptoShreddingWorkflowIdentity(
            WorkflowId: "01HKAAAAAAAAAAAAAAAAAAAAAA",
            TenantId: "t1",
            Domain: "orders",
            Scope: CryptoShreddingWorkflowScope.Aggregate,
            AggregateId: "agg-1",
            FromSequence: null,
            ToSequence: null,
            KeyReferencePolicy: KeyReferencePolicy.NoKeyReference,
            KeyAliasFingerprint: null);

        a.ShouldBe(b);
        a.GetHashCode().ShouldBe(b.GetHashCode());
    }

    [Fact]
    public void Identity_Validate_RejectsRangeScopeWithoutSequences() {
        var id = new CryptoShreddingWorkflowIdentity(
            WorkflowId: "01HKAAAAAAAAAAAAAAAAAAAAAA",
            TenantId: "t1",
            Domain: "orders",
            Scope: CryptoShreddingWorkflowScope.Range,
            AggregateId: "agg-1",
            FromSequence: null,
            ToSequence: null,
            KeyReferencePolicy: KeyReferencePolicy.NoKeyReference,
            KeyAliasFingerprint: null);
        bool ok = id.TryValidate(out string? reason);

        ok.ShouldBeFalse();
        reason.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Identity_Validate_RejectsKeyAliasFingerprintShape() {
        var id = new CryptoShreddingWorkflowIdentity(
            WorkflowId: "01HKAAAAAAAAAAAAAAAAAAAAAA",
            TenantId: "t1",
            Domain: "orders",
            Scope: CryptoShreddingWorkflowScope.Aggregate,
            AggregateId: "agg-1",
            FromSequence: null,
            ToSequence: null,
            KeyReferencePolicy: KeyReferencePolicy.AliasOnly,
            KeyAliasFingerprint: "not-hex-and-too-short");
        bool ok = id.TryValidate(out string? reason);

        ok.ShouldBeFalse();
        reason.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Identity_Validate_AcceptsValidAggregateScope() {
        var id = new CryptoShreddingWorkflowIdentity(
            WorkflowId: "01HKAAAAAAAAAAAAAAAAAAAAAA",
            TenantId: "t1",
            Domain: "orders",
            Scope: CryptoShreddingWorkflowScope.Aggregate,
            AggregateId: "agg-1",
            FromSequence: null,
            ToSequence: null,
            KeyReferencePolicy: KeyReferencePolicy.NoKeyReference,
            KeyAliasFingerprint: null);
        bool ok = id.TryValidate(out string? reason);

        ok.ShouldBeTrue(reason ?? "should be valid");
    }

    [Fact]
    public void Identity_Validate_RejectsUndefinedScope() {
        var id = new CryptoShreddingWorkflowIdentity(
            WorkflowId: "01HKAAAAAAAAAAAAAAAAAAAAAA",
            TenantId: "t1",
            Domain: "orders",
            Scope: (CryptoShreddingWorkflowScope)999,
            AggregateId: null,
            FromSequence: null,
            ToSequence: null,
            KeyReferencePolicy: KeyReferencePolicy.NoKeyReference,
            KeyAliasFingerprint: null);

        id.TryValidate(out string? reason).ShouldBeFalse();
        reason.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Identity_ComputeScopeKey_IgnoresWorkflowId_ForIdempotency() {
        var a = new CryptoShreddingWorkflowIdentity(
            "01HKAAAAAAAAAAAAAAAAAAAAAA",
            "t1",
            "orders",
            CryptoShreddingWorkflowScope.Aggregate,
            "agg-1",
            null,
            null,
            KeyReferencePolicy.NoKeyReference,
            null);
        var b = a with { WorkflowId = "01HKBBBBBBBBBBBBBBBBBBBBBB" };

        b.ComputeScopeKey().ShouldBe(a.ComputeScopeKey());
    }

    [Fact]
    public void Identity_ComputeKeyAliasFingerprint_IsDeterministicAndShort() {
        string a = CryptoShreddingWorkflowIdentity.ComputeKeyAliasFingerprint("tenant:t1:event-payload");
        string b = CryptoShreddingWorkflowIdentity.ComputeKeyAliasFingerprint("tenant:t1:event-payload");
        string c = CryptoShreddingWorkflowIdentity.ComputeKeyAliasFingerprint("tenant:t2:event-payload");

        a.ShouldBe(b);
        a.ShouldNotBe(c);
        a.Length.ShouldBe(CryptoShreddingWorkflowIdentity.KeyAliasFingerprintLength);
        foreach (char ch in a) {
            bool isHex = ch is (>= '0' and <= '9') or (>= 'a' and <= 'f');
            isHex.ShouldBeTrue();
        }
    }

    [Fact]
    public void Identity_ComputeKeyAliasFingerprint_NeverContainsRawAliasText() {
        // The raw alias must never leak into the fingerprint output.
        const string Alias = "tenant:t1:my-secret-key-alias-NO-LEAK";
        string fingerprint = CryptoShreddingWorkflowIdentity.ComputeKeyAliasFingerprint(Alias);

        fingerprint.ShouldNotContain("tenant", Case.Insensitive);
        fingerprint.ShouldNotContain("secret", Case.Insensitive);
        fingerprint.ShouldNotContain("alias", Case.Insensitive);
        fingerprint.ShouldNotContain("NO-LEAK", Case.Insensitive);
    }

    public static IEnumerable<object[]> AllStates() {
        foreach (CryptoShreddingWorkflowState state in Enum.GetValues<CryptoShreddingWorkflowState>()) {
            yield return new object[] { state };
        }
    }

    [Theory]
    [MemberData(nameof(AllStates))]
    public void Transitions_IsAllowed_ReturnsFalseForSelfTransition(CryptoShreddingWorkflowState state) {
        // Self-transitions are never explicitly allowed (they're treated as idempotent replays).
        CryptoShreddingWorkflowTransitions.IsAllowed(state, state).ShouldBeFalse();
    }

    [Fact]
    public void Transitions_TerminalStates_AreTerminal() {
        CryptoShreddingWorkflowTransitions.IsTerminal(CryptoShreddingWorkflowState.Rejected).ShouldBeTrue();
        CryptoShreddingWorkflowTransitions.IsTerminal(CryptoShreddingWorkflowState.Completed).ShouldBeTrue();
        CryptoShreddingWorkflowTransitions.IsTerminal(CryptoShreddingWorkflowState.CancelledBeforeDecision).ShouldBeTrue();
    }

    [Fact]
    public void Transitions_NonTerminalStates_AreNotTerminal() {
        CryptoShreddingWorkflowTransitions.IsTerminal(CryptoShreddingWorkflowState.Requested).ShouldBeFalse();
        CryptoShreddingWorkflowTransitions.IsTerminal(CryptoShreddingWorkflowState.PendingProvider).ShouldBeFalse();
        CryptoShreddingWorkflowTransitions.IsTerminal(CryptoShreddingWorkflowState.Invalidated).ShouldBeFalse();
    }

    [Fact]
    public void Transitions_IrreversibleDecision_CannotTransitionToCancelled() {
        CryptoShreddingWorkflowTransitions.IsIrreversibleDecision(CryptoShreddingWorkflowState.Invalidated).ShouldBeTrue();
        CryptoShreddingWorkflowTransitions.IsIrreversibleDecision(CryptoShreddingWorkflowState.Deleted).ShouldBeTrue();

        CryptoShreddingWorkflowTransitions.IsAllowed(
            CryptoShreddingWorkflowState.Invalidated,
            CryptoShreddingWorkflowState.CancelledBeforeDecision).ShouldBeFalse();
        CryptoShreddingWorkflowTransitions.IsAllowed(
            CryptoShreddingWorkflowState.Deleted,
            CryptoShreddingWorkflowState.CancelledBeforeDecision).ShouldBeFalse();
    }

    [Fact]
    public void Transitions_CancellationAllowedBeforeDecision() {
        CryptoShreddingWorkflowTransitions.IsCancellable(CryptoShreddingWorkflowState.Requested).ShouldBeTrue();
        CryptoShreddingWorkflowTransitions.IsCancellable(CryptoShreddingWorkflowState.Approved).ShouldBeTrue();
        CryptoShreddingWorkflowTransitions.IsCancellable(CryptoShreddingWorkflowState.PendingProvider).ShouldBeFalse();

        CryptoShreddingWorkflowTransitions.IsCancellable(CryptoShreddingWorkflowState.Invalidated).ShouldBeFalse();
        CryptoShreddingWorkflowTransitions.IsCancellable(CryptoShreddingWorkflowState.Deleted).ShouldBeFalse();
        CryptoShreddingWorkflowTransitions.IsCancellable(CryptoShreddingWorkflowState.Completed).ShouldBeFalse();
    }

    [Fact]
    public void Transitions_NormalProgress_IsAllowed() {
        CryptoShreddingWorkflowTransitions.IsAllowed(
            CryptoShreddingWorkflowState.Requested,
            CryptoShreddingWorkflowState.Approved).ShouldBeTrue();
        CryptoShreddingWorkflowTransitions.IsAllowed(
            CryptoShreddingWorkflowState.Approved,
            CryptoShreddingWorkflowState.PendingProvider).ShouldBeTrue();
        CryptoShreddingWorkflowTransitions.IsAllowed(
            CryptoShreddingWorkflowState.PendingProvider,
            CryptoShreddingWorkflowState.Invalidated).ShouldBeTrue();
        CryptoShreddingWorkflowTransitions.IsAllowed(
            CryptoShreddingWorkflowState.Invalidated,
            CryptoShreddingWorkflowState.Completed).ShouldBeTrue();
    }

    [Fact]
    public void Decision_ReasonCodeFor_AllStates_AreStableAndUnique() {
        var codes = new HashSet<string>(StringComparer.Ordinal);
        foreach (CryptoShreddingWorkflowState state in Enum.GetValues<CryptoShreddingWorkflowState>()) {
            string code = CryptoShreddingWorkflowDecision.ReasonCodeFor(state);
            code.ShouldNotBeNullOrWhiteSpace();
            code.ShouldBe(code.ToLowerInvariant());
            codes.Add(code).ShouldBeTrue($"Duplicate code {code} for state {state}.");
        }
    }

    [Fact]
    public void Decision_NextActionFor_UnknownState_Throws() {
        _ = Should.Throw<ArgumentOutOfRangeException>(
            () => CryptoShreddingWorkflowDecision.NextActionFor((CryptoShreddingWorkflowState)999));
    }

    [Fact]
    public void Request_TryValidate_RejectsNonTerminalAction() {
        var id = new CryptoShreddingWorkflowIdentity(
            "01HKAAAAAAAAAAAAAAAAAAAAAA",
            "t1",
            "orders",
            CryptoShreddingWorkflowScope.Aggregate,
            "agg-1",
            null,
            null,
            KeyReferencePolicy.NoKeyReference,
            null);
        var request = new CryptoShreddingWorkflowRequest(
            id,
            CryptoShreddingWorkflowState.Requested,
            "operator",
            null,
            DateTimeOffset.UtcNow);

        request.TryValidate(out string? reason).ShouldBeFalse();
        reason.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Audit_TryValidate_RequiresWorkflowOrAdmissionTransition() {
        var audit = new CryptoShreddingAuditEvent(
            AuditId: "01HK",
            WorkflowId: null,
            AdmissionId: null,
            TenantId: "t1",
            Domain: "orders",
            AggregateId: "agg-1",
            FromSequence: null,
            ToSequence: null,
            ProtectionMetadataVersion: 1,
            KeyReferencePolicy: KeyReferencePolicy.NoKeyReference,
            KeyAliasFingerprint: null,
            WorkflowFromState: null,
            WorkflowToState: null,
            AdmissionFromState: null,
            AdmissionToState: null,
            DecisionActorId: "operator",
            CorrelationId: null,
            DecidedAtUtc: DateTimeOffset.UtcNow,
            ReasonCode: "rejected");

        audit.TryValidate(out string? reason).ShouldBeFalse();
        reason.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Audit_TryValidate_RejectsForbiddenKeyAliasFingerprintShape() {
        var audit = new CryptoShreddingAuditEvent(
            AuditId: "01HK",
            WorkflowId: "01HKWF",
            AdmissionId: null,
            TenantId: "t1",
            Domain: "orders",
            AggregateId: "agg-1",
            FromSequence: null,
            ToSequence: null,
            ProtectionMetadataVersion: 1,
            KeyReferencePolicy: KeyReferencePolicy.AliasOnly,
            KeyAliasFingerprint: "TOO-LONG-NOT-HEX-NEVER",
            WorkflowFromState: CryptoShreddingWorkflowState.Requested,
            WorkflowToState: CryptoShreddingWorkflowState.Approved,
            AdmissionFromState: null,
            AdmissionToState: null,
            DecisionActorId: "operator",
            CorrelationId: null,
            DecidedAtUtc: DateTimeOffset.UtcNow,
            ReasonCode: "approved");

        audit.TryValidate(out string? reason).ShouldBeFalse();
        reason.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Audit_TryValidate_RejectsNonHexFingerprintWithValidLength() {
        var audit = new CryptoShreddingAuditEvent(
            AuditId: "01HK",
            WorkflowId: "01HKWF",
            AdmissionId: null,
            TenantId: "t1",
            Domain: "orders",
            AggregateId: "agg-1",
            FromSequence: null,
            ToSequence: null,
            ProtectionMetadataVersion: 1,
            KeyReferencePolicy: KeyReferencePolicy.AliasOnly,
            KeyAliasFingerprint: "zzzzzzzzzzzzzzzz",
            WorkflowFromState: CryptoShreddingWorkflowState.Requested,
            WorkflowToState: CryptoShreddingWorkflowState.Approved,
            AdmissionFromState: null,
            AdmissionToState: null,
            DecisionActorId: "operator",
            CorrelationId: null,
            DecidedAtUtc: DateTimeOffset.UtcNow,
            ReasonCode: "approved");

        audit.TryValidate(out string? reason).ShouldBeFalse();
        reason.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Audit_TryValidate_RejectsNegativeToSequence_WhenFromSequenceMissing() {
        var audit = new CryptoShreddingAuditEvent(
            AuditId: "01HK",
            WorkflowId: "01HKWF",
            AdmissionId: null,
            TenantId: "t1",
            Domain: "orders",
            AggregateId: "agg-1",
            FromSequence: null,
            ToSequence: -1,
            ProtectionMetadataVersion: 1,
            KeyReferencePolicy: KeyReferencePolicy.NoKeyReference,
            KeyAliasFingerprint: null,
            WorkflowFromState: CryptoShreddingWorkflowState.Requested,
            WorkflowToState: CryptoShreddingWorkflowState.Approved,
            AdmissionFromState: null,
            AdmissionToState: null,
            DecisionActorId: "operator",
            CorrelationId: null,
            DecidedAtUtc: DateTimeOffset.UtcNow,
            ReasonCode: "approved");

        audit.TryValidate(out string? reason).ShouldBeFalse();
        reason.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Audit_JsonRoundTrip_PreservesContract() {
        var audit = new CryptoShreddingAuditEvent(
            AuditId: "01HKAUDIT",
            WorkflowId: "01HKWF",
            AdmissionId: null,
            TenantId: "t1",
            Domain: "orders",
            AggregateId: "agg-1",
            FromSequence: 10,
            ToSequence: 20,
            ProtectionMetadataVersion: 1,
            KeyReferencePolicy: KeyReferencePolicy.NoKeyReference,
            KeyAliasFingerprint: null,
            WorkflowFromState: CryptoShreddingWorkflowState.Requested,
            WorkflowToState: CryptoShreddingWorkflowState.Approved,
            AdmissionFromState: null,
            AdmissionToState: null,
            DecisionActorId: "operator",
            CorrelationId: "01HKCORR",
            DecidedAtUtc: DateTimeOffset.Parse("2026-05-18T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
            ReasonCode: "approved");

        string json = JsonSerializer.Serialize(audit);
        CryptoShreddingAuditEvent? round = JsonSerializer.Deserialize<CryptoShreddingAuditEvent>(json);

        round.ShouldNotBeNull();
        round.ShouldBe(audit);
    }
}
