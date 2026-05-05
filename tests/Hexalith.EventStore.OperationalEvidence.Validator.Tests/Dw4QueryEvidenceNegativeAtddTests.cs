using Hexalith.EventStore.OperationalEvidence.Validator.Tests.Fixtures;

using Shouldly;

namespace Hexalith.EventStore.OperationalEvidence.Validator.Tests;

/// <summary>
/// DW4 ATDD red-phase scaffolds for query/v1 negative fixtures (AC #2, #3, #4, #5, #6, #13).
/// Each test asserts both that the fixture fails AND that the expected rule id
/// is among the emitted diagnostics. Asserting expected rule ids prevents an
/// unrelated parser failure from masquerading as rule coverage (AC #8 /
/// Advanced Elicitation).
/// </summary>
public class Dw4QueryEvidenceNegativeAtddTests {
    private const string _baseSkip = "ATDD red phase — DW4 ";

    [Fact(Skip = _baseSkip + "AC#2 — missing required metadata field must fail with query-required-metadata-missing. Remove Skip when implementing.")]
    public void QueryEvidence_MissingRequiredMetadata_Fails() {
        AssertFixture(
            "query-invalid-missing-metadata.md",
            Dw4RuleVocabulary.QueryRequiredMetadataMissing);
    }

    [Fact(Skip = _baseSkip + "AC#3 — unreplaced angle-bracket placeholder must fail with placeholder-unreplaced. Remove Skip when implementing.")]
    public void QueryEvidence_PlaceholderUnreplaced_Fails() {
        AssertFixture(
            "query-invalid-placeholder-unreplaced.md",
            Dw4RuleVocabulary.PlaceholderUnreplaced);
    }

    [Fact(Skip = _baseSkip + "AC#3 — empty required table cell must fail with required-table-cell-empty. Remove Skip when implementing.")]
    public void QueryEvidence_EmptyRequiredTableCell_Fails() {
        AssertFixture(
            "query-invalid-empty-required-table-cell.md",
            Dw4RuleVocabulary.RequiredTableCellEmpty);
    }

    [Fact(Skip = _baseSkip + "AC#4 — classification outside the 9-value enum must fail with classification-invalid. Remove Skip when implementing.")]
    public void QueryEvidence_ClassificationNotInEnum_Fails() {
        AssertFixture(
            "query-invalid-classification-not-in-enum.md",
            Dw4RuleVocabulary.ClassificationInvalid);
    }

    [Fact(Skip = _baseSkip + "AC#6 — missing false-positive control must fail with control-required-missing. Remove Skip when implementing.")]
    public void QueryEvidence_FalsePositiveControlMissing_Fails() {
        AssertFixture(
            "query-invalid-control-missing.md",
            Dw4RuleVocabulary.ControlRequiredMissing);
    }

    [Fact(Skip = _baseSkip + "AC#6 — missing correlation-integrity control must fail with correlation-control-required-missing. Remove Skip when implementing.")]
    public void QueryEvidence_CorrelationControlMissing_Fails() {
        AssertFixture(
            "query-invalid-correlation-control-missing.md",
            Dw4RuleVocabulary.CorrelationControlRequiredMissing);
    }

    private static void AssertFixture(string fixtureFileName, string expectedRuleId) {
        Dw4FixtureCatalog.FixtureExpectation expected = Dw4FixtureCatalog.ByName[fixtureFileName];
        expected.ExpectedVerdict.ShouldBe(Dw4FixtureCatalog.Verdict.Fail);
        expected.ExpectedRuleIds.ShouldContain(expectedRuleId,
            $"Catalog drift: fixture '{fixtureFileName}' should have '{expectedRuleId}' " +
            "in its ExpectedRuleIds set. Update Dw4FixtureCatalog and this assertion together.");

        IDw4ValidatorInvoker invoker = Dw4ValidatorInvokerFactory.Create();
        Dw4ValidationOutcome outcome = invoker.Validate([
            Path.Combine(Dw4FixtureCatalog.FixtureRoot, fixtureFileName),
        ]);

        outcome.Passed.ShouldBeFalse($"Fixture '{fixtureFileName}' must fail. {expected.Notes}");
        outcome.ExitCode.ShouldNotBe(0);
        outcome.EmittedRuleIds.ShouldContain(expectedRuleId,
            $"Fixture '{fixtureFileName}' must emit rule '{expectedRuleId}'. {expected.Notes}");
    }
}
