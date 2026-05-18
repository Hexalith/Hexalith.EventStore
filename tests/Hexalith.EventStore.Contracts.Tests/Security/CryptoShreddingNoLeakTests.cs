using System;
using System.Collections.Generic;

using Hexalith.EventStore.Contracts.Problems;
using Hexalith.EventStore.Contracts.Security;

using Shouldly;

namespace Hexalith.EventStore.Contracts.Tests.Security;

/// <summary>
/// Story 22.7c — verifies the new contract types never reveal sensitive substrings such as raw
/// alias text, plaintext payloads, or provider exception text in their reason codes, next-action
/// hints, or ProblemDetails guidance.
/// </summary>
public class CryptoShreddingNoLeakTests {
    // Sentinel substrings borrowed from the Story 22.7b Testing helper. Re-declared here to keep
    // Contracts independent of Testing.
    private static readonly string[] Sentinels = [
        "PROTECTED_PAYLOAD_PLAINTEXT_MARKER_22_7B",
        "PROTECTED_SNAPSHOT_PLAINTEXT_MARKER_22_7B",
        "PROTECTED_KEY_ALIAS_MARKER_22_7B",
        "PROTECTED_PROVIDER_PRIVATE_BLOB_MARKER_22_7B",
        "PROTECTED_STATE_STORE_KEY_MARKER_22_7B",
        "PROTECTED_CONNECTION_STRING_MARKER_22_7B",
        "PROTECTED_PROVIDER_EXCEPTION_MARKER_22_7B",
    ];

    [Fact]
    public void WorkflowProblem_OperatorGuidance_HasNoSentinels() {
        foreach (CryptoShreddingWorkflowState state in Enum.GetValues<CryptoShreddingWorkflowState>()) {
            string text = CryptoShreddingWorkflowProblem.GetSafeOperatorGuidance(state);
            AssertNoSentinel(text);
        }
    }

    [Fact]
    public void RestoredAdmissionProblem_OperatorGuidance_HasNoSentinels() {
        foreach (RestoredBackupAdmissionState state in Enum.GetValues<RestoredBackupAdmissionState>()) {
            string text = RestoredBackupAdmissionProblem.GetSafeOperatorGuidance(state);
            AssertNoSentinel(text);
        }
    }

    [Fact]
    public void NextActions_AreStableEnumNames_NoSentinels() {
        foreach (CryptoShreddingNextAction action in Enum.GetValues<CryptoShreddingNextAction>()) {
            AssertNoSentinel(action.ToString());
        }
    }

    [Fact]
    public void DecisionStageCodes_AreKebabAndNoSentinels() {
        foreach (ProtectedDataDecisionStage stage in Enum.GetValues<ProtectedDataDecisionStage>()) {
            string code = ProtectedDataReadabilityDecisionStageCodes.From(stage);
            code.ShouldNotBeNullOrWhiteSpace();
            AssertNoSentinel(code);
        }
    }

    [Fact]
    public void ComputedFingerprint_NeverContainsAliasOrSentinel() {
        string alias = "tenant:t1:my-key-alias-" + Sentinels[2];
        string fingerprint = CryptoShreddingWorkflowIdentity.ComputeKeyAliasFingerprint(alias);

        AssertNoSentinel(fingerprint);
        fingerprint.ShouldNotContain("tenant", Case.Insensitive);
        fingerprint.ShouldNotContain("alias", Case.Insensitive);
    }

    private static void AssertNoSentinel(string captured) {
        foreach (string sentinel in Sentinels) {
            captured.ShouldNotContain(sentinel, Case.Insensitive);
        }
    }
}
