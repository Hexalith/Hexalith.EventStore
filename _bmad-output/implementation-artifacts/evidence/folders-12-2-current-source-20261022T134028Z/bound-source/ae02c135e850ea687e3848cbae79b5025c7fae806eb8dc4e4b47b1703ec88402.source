using Hexalith.EventStore.OperationalEvidence.Validator.Tests.Fixtures;

using Shouldly;

namespace Hexalith.EventStore.OperationalEvidence.Validator.Tests;

/// <summary>
/// DW4 ATDD red-phase scaffolds for AC #2 / Party-Mode hardening — the
/// 'not-applicable: &lt;reason&gt;' marker is allowed only on profile-specific or
/// optional fields, the reason must be non-empty/specific, and the marker is
/// not a legal value of the run-level classification.
/// </summary>
public class Dw4NotApplicableMarkerAtddTests {
    private const string _baseSkip = "ATDD red phase — DW4 ";

    [Fact(Skip = _baseSkip + "AC#2 — 'not-applicable:' with empty reason must fail. Remove Skip when implementing.")]
    public void NotApplicable_EmptyReason_FailsWithReasonMissing() {
        Dw4FixtureCatalog.FixtureExpectation expected = Dw4FixtureCatalog.ByName[
            "query-invalid-not-applicable-empty-reason.md"];
        expected.ExpectedRuleIds.ShouldContain(Dw4RuleVocabulary.NotApplicableReasonMissing);

        IDw4ValidatorInvoker invoker = Dw4ValidatorInvokerFactory.Create();
        Dw4ValidationOutcome outcome = invoker.Validate([
            Path.Combine(Dw4FixtureCatalog.FixtureRoot, "query-invalid-not-applicable-empty-reason.md"),
        ]);

        outcome.Passed.ShouldBeFalse();
        outcome.EmittedRuleIds.ShouldContain(Dw4RuleVocabulary.NotApplicableReasonMissing,
            "AC #2: 'not-applicable:' must carry an explicit one-line reason. " +
            "An empty/generic reason must fail closed.");
    }

    [Fact(Skip = _baseSkip + "AC#2 — 'not-applicable: <reason>' on a non-profile required field must fail. Remove Skip when implementing.")]
    public void NotApplicable_OnNonProfileRequiredField_FailsWithNotAllowedHere() {
        Dw4FixtureCatalog.FixtureExpectation expected = Dw4FixtureCatalog.ByName[
            "query-invalid-not-applicable-on-required-field.md"];
        expected.ExpectedRuleIds.ShouldContain(Dw4RuleVocabulary.NotApplicableNotAllowedHere);

        IDw4ValidatorInvoker invoker = Dw4ValidatorInvokerFactory.Create();
        Dw4ValidationOutcome outcome = invoker.Validate([
            Path.Combine(Dw4FixtureCatalog.FixtureRoot, "query-invalid-not-applicable-on-required-field.md"),
        ]);

        outcome.Passed.ShouldBeFalse();
        outcome.EmittedRuleIds.ShouldContain(Dw4RuleVocabulary.NotApplicableNotAllowedHere,
            "AC #2 / Party-Mode: 'not-applicable' must be rejected when used on a field " +
            "the schema marks required-non-optional (e.g., schema_version, evidence_run_id).");
    }
}
