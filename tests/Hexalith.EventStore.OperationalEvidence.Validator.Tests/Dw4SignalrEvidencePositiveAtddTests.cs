using Hexalith.EventStore.OperationalEvidence.Validator.Tests.Fixtures;

using Shouldly;

namespace Hexalith.EventStore.OperationalEvidence.Validator.Tests;

/// <summary>
/// DW4 ATDD red-phase scaffolds for signalr/v1 positive fixtures (AC #1, #2, #7).
/// Proves the validator does NOT over-fail a complete SignalR evidence file.
/// </summary>
public class Dw4SignalrEvidencePositiveAtddTests {
    private const string _baseSkip = "ATDD red phase — DW4 ";

    [Fact(Skip = _baseSkip + "AC#1, #2 — minimal valid signalr/v1 evidence must pass. Remove Skip when implementing.")]
    public void SignalrEvidence_MinimalValidFixture_Passes() {
        IDw4ValidatorInvoker invoker = Dw4ValidatorInvokerFactory.Create();

        Dw4ValidationOutcome outcome = invoker.Validate([
            Path.Combine(Dw4FixtureCatalog.FixtureRoot, "signalr-valid-minimal.md"),
        ]);

        outcome.Passed.ShouldBeTrue(
            "A minimal-but-complete signalr/v1 fixture must pass — over-failing would make " +
            "the validator unusable as a docs-validation gate.");
        outcome.Diagnostics.ShouldBeEmpty();
        outcome.ExitCode.ShouldBe(0);
    }
}
