using Hexalith.EventStore.OperationalEvidence.Validator.Tests.Fixtures;

using Shouldly;

namespace Hexalith.EventStore.OperationalEvidence.Validator.Tests;

/// <summary>
/// DW4 ATDD red-phase scaffolds for the validator-contract perimeter (AC #1, #2, #8, #11, #13).
/// These tests prove that the validator entrypoint exists, accepts the documented
/// invocation contract, and processes every catalogued fixture with the expected
/// pass/fail outcome and the expected rule-id set. They do not duplicate the
/// per-rule assertions that live in their own dedicated test files.
/// </summary>
public class Dw4ValidatorContractAtddTests {
    private const string _baseSkip = "ATDD red phase — DW4 ";

    [Fact(Skip = _baseSkip + "AC#1 — validator entrypoint must be declared via entrypoint.txt. Remove Skip when wiring.")]
    public void ValidatorEntrypoint_MustBeDeclaredAndResolveToInvoker() {
        IDw4ValidatorInvoker invoker = Dw4ValidatorInvokerFactory.Create();

        invoker.ShouldNotBeNull();
        invoker.EntrypointDescription.ShouldNotBeNullOrWhiteSpace(
            "Validator entrypoint must self-describe so reviewers can audit which validator produced diagnostics.");
    }

    [Fact(Skip = _baseSkip + "AC#1, #11 — default scope is templates + curated fixtures only. Remove Skip when implementing.")]
    public void ValidatorDefaultScope_DoesNotRecursivelyAuditHistoricalEvidence() {
        IDw4ValidatorInvoker invoker = Dw4ValidatorInvokerFactory.Create();

        // The validator must NOT, by default, walk the entire repo for any markdown
        // file with placeholder shapes. AC #11 forbids mass historical rewrites.
        // This test invokes the validator with no fixture paths and asserts it
        // either no-ops (exit 0, zero diagnostics) or fails with a clear
        // 'no-input' diagnostic — never that it picked up unrelated files.
        Dw4ValidationOutcome outcome = invoker.Validate([]);

        outcome.Diagnostics.Where(d => !string.IsNullOrEmpty(d.File))
            .ShouldBeEmpty("validator must not process files when given no inputs (AC #11).");
    }

    [Theory(Skip = _baseSkip + "AC#8 — every fixture in catalog must produce its expected verdict and rule-id set. Remove Skip when implementing.")]
    [MemberData(nameof(FixtureCatalogRows))]
    public void Fixture_ProducesExpectedVerdictAndRuleIds(string fixtureFileName) {
        Dw4FixtureCatalog.FixtureExpectation expected = Dw4FixtureCatalog.ByName[fixtureFileName];
        IDw4ValidatorInvoker invoker = Dw4ValidatorInvokerFactory.Create();

        string fixturePath = Path.Combine(Dw4FixtureCatalog.FixtureRoot, fixtureFileName);
        Dw4ValidationOutcome outcome = invoker.Validate([fixturePath]);

        if (expected.ExpectedVerdict == Dw4FixtureCatalog.Verdict.Pass) {
            outcome.Passed.ShouldBeTrue(
                $"Fixture '{fixtureFileName}' must validate as pass. Notes: {expected.Notes}");
            outcome.Diagnostics.ShouldBeEmpty();
        }
        else {
            outcome.Passed.ShouldBeFalse(
                $"Fixture '{fixtureFileName}' is a negative fixture and must fail. Notes: {expected.Notes}");
            outcome.ExitCode.ShouldNotBe(0,
                "Validator must exit non-zero when any diagnostic is emitted (AC #13).");
            foreach (string ruleId in expected.ExpectedRuleIds) {
                outcome.EmittedRuleIds.ShouldContain(
                    ruleId,
                    $"Fixture '{fixtureFileName}' must emit rule '{ruleId}'. Asserting expected rule ids " +
                    $"prevents unrelated parser/regression failures from masquerading as rule coverage " +
                    $"(AC #8 / Advanced Elicitation).");
            }
        }
    }

    [Fact(Skip = _baseSkip + "AC#8 — every rule family in the vocabulary must be exercised by at least one fixture. Remove Skip when implementing.")]
    public void FixtureCoverageMatrix_EveryNonPassRuleFamilyHasAtLeastOneFixture() {
        // AC #8 fixture coverage matrix: every rule id the validator commits to
        // (other than schema-version and parse families covered by their own
        // suites) must have at least one negative fixture exercising it.
        IReadOnlySet<string> covered = Dw4FixtureCatalog.CoveredRuleIds;
        IEnumerable<string> uncovered = Dw4RuleVocabulary.All.Where(r => !covered.Contains(r));

        uncovered.ShouldBeEmpty(
            "Every rule id in Dw4RuleVocabulary must have a fixture in Dw4FixtureCatalog. " +
            "Add a covering negative fixture for each missing rule before closing the story.");
    }

    public static TheoryData<string> FixtureCatalogRows() {
        TheoryData<string> data = [];
        foreach (Dw4FixtureCatalog.FixtureExpectation f in Dw4FixtureCatalog.All) {
            data.Add(f.FileName);
        }

        return data;
    }
}
