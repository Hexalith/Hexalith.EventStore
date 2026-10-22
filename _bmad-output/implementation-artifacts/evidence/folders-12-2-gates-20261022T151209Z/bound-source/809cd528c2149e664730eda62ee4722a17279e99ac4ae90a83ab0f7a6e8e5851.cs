using Hexalith.EventStore.OperationalEvidence.Validator.Tests.Fixtures;

using Shouldly;

namespace Hexalith.EventStore.OperationalEvidence.Validator.Tests;

/// <summary>
/// DW4 ATDD red-phase scaffolds for query/v1 positive fixtures (AC #1, #2, #7).
/// Proves the validator does NOT over-fail: a complete, properly-redacted query
/// evidence file passes; a non-Aspire proof with explicit 'not-applicable: &lt;reason&gt;'
/// markers also passes.
/// </summary>
public class Dw4QueryEvidencePositiveAtddTests {
    private const string _baseSkip = "ATDD red phase — DW4 ";

    [Fact(Skip = _baseSkip + "AC#1, #2 — minimal valid query/v1 evidence must pass. Remove Skip when implementing.")]
    public void QueryEvidence_MinimalValidFixture_Passes() {
        IDw4ValidatorInvoker invoker = Dw4ValidatorInvokerFactory.Create();

        Dw4ValidationOutcome outcome = invoker.Validate([
            Path.Combine(Dw4FixtureCatalog.FixtureRoot, "query-valid-minimal.md"),
        ]);

        outcome.Passed.ShouldBeTrue(
            "A minimal-but-complete query/v1 fixture must pass — over-failing would make " +
            "the validator unusable as a docs-validation gate.");
        outcome.Diagnostics.ShouldBeEmpty();
        outcome.ExitCode.ShouldBe(0);
    }

    [Fact(Skip = _baseSkip + "AC#7 — Aspire fields with 'not-applicable: <reason>' must pass. Remove Skip when implementing.")]
    public void QueryEvidence_NotApplicableAspireFields_Passes() {
        IDw4ValidatorInvoker invoker = Dw4ValidatorInvokerFactory.Create();

        Dw4ValidationOutcome outcome = invoker.Validate([
            Path.Combine(Dw4FixtureCatalog.FixtureRoot, "query-valid-not-applicable-aspire.md"),
        ]);

        outcome.Passed.ShouldBeTrue(
            "AC #7: Aspire fields are profile-scoped. When a non-Aspire proof marks them " +
            "'not-applicable: <reason>', the validator must accept the file.");
        outcome.Diagnostics.ShouldBeEmpty();
    }
}
