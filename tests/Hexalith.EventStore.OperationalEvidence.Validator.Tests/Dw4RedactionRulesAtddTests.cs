using Hexalith.EventStore.OperationalEvidence.Validator.Tests.Fixtures;

using Shouldly;

namespace Hexalith.EventStore.OperationalEvidence.Validator.Tests;

/// <summary>
/// DW4 ATDD red-phase scaffolds for AC #5 — redaction rules. Bearer tokens,
/// connection-string keywords, production hostnames, and raw secret markers
/// must fail unless explicitly redacted with documented synthetic markers.
/// Documented escape paths for known false positives must work.
/// </summary>
public class Dw4RedactionRulesAtddTests {
    private const string _baseSkip = "ATDD red phase — DW4 ";

    [Fact(Skip = _baseSkip + "AC#5 — JWT-shaped bearer token outside redaction marker must fail. Remove Skip when implementing.")]
    public void Redaction_BearerTokenInDiagnosticsText_Fails() {
        AssertFixture(
            "query-invalid-redaction-bearer-token.md",
            Dw4RuleVocabulary.RedactionUnsafeBearerToken);
    }

    [Fact(Skip = _baseSkip + "AC#5 — connection-string keyword must fail. Remove Skip when implementing.")]
    public void Redaction_ConnectionStringKeyword_Fails() {
        AssertFixture(
            "query-invalid-redaction-connection-string.md",
            Dw4RuleVocabulary.RedactionUnsafeConnectionString);
    }

    [Fact(Skip = _baseSkip + "AC#5 — production hostname pattern must fail. Remove Skip when implementing.")]
    public void Redaction_ProductionHostname_Fails() {
        AssertFixture(
            "query-invalid-redaction-production-hostname.md",
            Dw4RuleVocabulary.RedactionUnsafeProductionHostname);
    }

    [Fact(Skip = _baseSkip + "AC#5 — missing Redaction section must fail with redaction-section-missing. Remove Skip when implementing.")]
    public void Redaction_SectionMissing_Fails() {
        AssertFixture(
            "query-invalid-redaction-section-missing.md",
            Dw4RuleVocabulary.RedactionSectionMissing);
    }

    [Fact(Skip = _baseSkip + "AC#5 — raw secret marker (AKIA…, xoxb-, GitHub PAT shape) must fail. Remove Skip when implementing.")]
    public void Redaction_RawSecretMarker_Fails() {
        AssertFixture(
            "query-invalid-raw-secret-marker.md",
            Dw4RuleVocabulary.RedactionRawSecretMarker);
    }

    [Fact(Skip = _baseSkip + "AC#5 — SignalR fixture with bearer token must also fail. Remove Skip when implementing.")]
    public void Redaction_SignalrBearerToken_Fails() {
        AssertFixture(
            "signalr-invalid-redaction-bearer-token.md",
            Dw4RuleVocabulary.RedactionUnsafeBearerToken);
    }

    [Fact(Skip = _baseSkip + "AC#5 — documented synthetic markers under Redaction must NOT trigger false positives. Remove Skip when implementing.")]
    public void Redaction_DocumentedSyntheticMarkers_Pass() {
        // Reusing the minimal-valid query fixture which uses 'tenant-alias-001',
        // 'safe-domain-alias' synthetic patterns and a Redaction section that
        // declares them safe-to-commit.
        IDw4ValidatorInvoker invoker = Dw4ValidatorInvokerFactory.Create();
        Dw4ValidationOutcome outcome = invoker.Validate([
            Path.Combine(Dw4FixtureCatalog.FixtureRoot, "query-valid-minimal.md"),
        ]);

        outcome.Passed.ShouldBeTrue(
            "Documented synthetic markers ('tenant-alias-001', '<redacted>', etc.) used under " +
            "a present Redaction section must not trip redaction rules. AC #5 explicitly " +
            "requires false-positive escape guidance.");
    }

    private static void AssertFixture(string fixtureFileName, string expectedRuleId) {
        Dw4FixtureCatalog.FixtureExpectation expected = Dw4FixtureCatalog.ByName[fixtureFileName];
        expected.ExpectedVerdict.ShouldBe(Dw4FixtureCatalog.Verdict.Fail);
        expected.ExpectedRuleIds.ShouldContain(expectedRuleId);

        IDw4ValidatorInvoker invoker = Dw4ValidatorInvokerFactory.Create();
        Dw4ValidationOutcome outcome = invoker.Validate([
            Path.Combine(Dw4FixtureCatalog.FixtureRoot, fixtureFileName),
        ]);

        outcome.Passed.ShouldBeFalse($"Fixture '{fixtureFileName}' must fail. {expected.Notes}");
        outcome.EmittedRuleIds.ShouldContain(expectedRuleId,
            $"Fixture '{fixtureFileName}' must emit '{expectedRuleId}'. {expected.Notes}");
    }
}
