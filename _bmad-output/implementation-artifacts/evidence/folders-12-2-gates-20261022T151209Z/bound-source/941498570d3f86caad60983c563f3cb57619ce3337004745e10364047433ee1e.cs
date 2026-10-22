using Hexalith.EventStore.OperationalEvidence.Validator.Tests.Fixtures;

using Shouldly;

namespace Hexalith.EventStore.OperationalEvidence.Validator.Tests;

/// <summary>
/// DW4 ATDD red-phase scaffolds for AC #7 — Aspire-specific evidence is
/// optional or profile-scoped. Non-Aspire proofs may mark Aspire fields
/// 'not-applicable: &lt;reason&gt;' and pass; Aspire-claimed proofs must fill them.
/// </summary>
public class Dw4ProfileScopedAspireAtddTests {
    private const string _baseSkip = "ATDD red phase — DW4 ";

    [Fact(Skip = _baseSkip + "AC#7 — non-Aspire proof with not-applicable Aspire fields passes. Remove Skip when implementing.")]
    public void Profile_NonAspireWithNotApplicableFields_Passes() {
        IDw4ValidatorInvoker invoker = Dw4ValidatorInvokerFactory.Create();
        Dw4ValidationOutcome outcome = invoker.Validate([
            Path.Combine(Dw4FixtureCatalog.FixtureRoot, "query-valid-not-applicable-aspire.md"),
        ]);

        outcome.Passed.ShouldBeTrue(
            "AC #7: an Aspire field marked 'not-applicable: <reason>' for a non-Aspire " +
            "proof must be accepted as long as the rest of the schema is complete.");
    }

    [Fact(Skip = _baseSkip + "AC#7 — Aspire-claimed proof with blank Aspire fields must fail. Remove Skip when implementing.")]
    public void Profile_AspireClaimedButFieldsMissing_FailsWithProfileAspireFieldsMissing() {
        Dw4FixtureCatalog.FixtureExpectation expected = Dw4FixtureCatalog.ByName[
            "query-invalid-aspire-claimed-but-fields-missing.md"];
        expected.ExpectedRuleIds.ShouldContain(Dw4RuleVocabulary.ProfileAspireFieldsMissing);

        IDw4ValidatorInvoker invoker = Dw4ValidatorInvokerFactory.Create();
        Dw4ValidationOutcome outcome = invoker.Validate([
            Path.Combine(Dw4FixtureCatalog.FixtureRoot, "query-invalid-aspire-claimed-but-fields-missing.md"),
        ]);

        outcome.Passed.ShouldBeFalse();
        outcome.EmittedRuleIds.ShouldContain(Dw4RuleVocabulary.ProfileAspireFieldsMissing,
            "AC #7: when a proof references Aspire/DAPR runtime, AppHost/placement/sidecar " +
            "fields become required. Blank fields with no 'not-applicable' marker must fail.");
    }
}
