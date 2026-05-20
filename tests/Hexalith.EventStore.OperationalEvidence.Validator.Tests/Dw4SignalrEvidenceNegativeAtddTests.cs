using Hexalith.EventStore.OperationalEvidence.Validator.Tests.Fixtures;

using Shouldly;

namespace Hexalith.EventStore.OperationalEvidence.Validator.Tests;

/// <summary>
/// DW4 ATDD red-phase scaffolds for signalr/v1 negative fixtures (AC #2, #3, #4, #5, #6).
/// Each test asserts the fixture fails AND emits the expected rule id.
/// </summary>
public class Dw4SignalrEvidenceNegativeAtddTests {
    private const string _baseSkip = "ATDD red phase — DW4 ";

    [Fact(Skip = _baseSkip + "AC#2 — missing required SignalR metadata must fail with signalr-required-metadata-missing. Remove Skip when implementing.")]
    public void SignalrEvidence_MissingRequiredMetadata_Fails() => AssertFixture(
            "signalr-invalid-missing-metadata.md",
            Dw4RuleVocabulary.SignalrRequiredMetadataMissing);

    [Fact(Skip = _baseSkip + "AC#3 — placeholder unreplaced must fail with placeholder-unreplaced. Remove Skip when implementing.")]
    public void SignalrEvidence_PlaceholderUnreplaced_Fails() => AssertFixture(
            "signalr-invalid-placeholder-unreplaced.md",
            Dw4RuleVocabulary.PlaceholderUnreplaced);

    [Fact(Skip = _baseSkip + "AC#4 — classification outside the 6-value SignalR enum must fail. Remove Skip when implementing.")]
    public void SignalrEvidence_ClassificationNotInEnum_Fails() => AssertFixture(
            "signalr-invalid-classification-not-in-enum.md",
            Dw4RuleVocabulary.ClassificationInvalid);

    [Fact(Skip = _baseSkip + "AC#6 — missing false-positive control must fail. Remove Skip when implementing.")]
    public void SignalrEvidence_ControlMissing_Fails() => AssertFixture(
            "signalr-invalid-control-missing.md",
            Dw4RuleVocabulary.ControlRequiredMissing);

    private static void AssertFixture(string fixtureFileName, string expectedRuleId) {
        Dw4FixtureCatalog.FixtureExpectation expected = Dw4FixtureCatalog.ByName[fixtureFileName];
        expected.ExpectedVerdict.ShouldBe(Dw4FixtureCatalog.Verdict.Fail);
        expected.ExpectedRuleIds.ShouldContain(expectedRuleId);

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
