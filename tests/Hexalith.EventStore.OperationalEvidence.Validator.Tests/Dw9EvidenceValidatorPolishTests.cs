using Hexalith.EventStore.OperationalEvidence.Validator.Tests.Fixtures;

using Shouldly;

namespace Hexalith.EventStore.OperationalEvidence.Validator.Tests;

public class Dw9EvidenceValidatorPolishTests {
    [Theory]
    [InlineData("query-invalid-control-linkage-missing.md", Dw4RuleVocabulary.ControlLinkageMissing, "false_positive_control")]
    [InlineData("query-invalid-control-linkage-unrelated.md", Dw4RuleVocabulary.ControlLinkageUnrelated, "false_positive_control")]
    [InlineData("signalr-invalid-control-linkage-missing.md", Dw4RuleVocabulary.ControlLinkageMissing, "reliability_control")]
    [InlineData("signalr-invalid-control-linkage-unrelated.md", Dw4RuleVocabulary.ControlLinkageUnrelated, "reliability_control")]
    public void ControlLinkageFixtures_EmitPreciseDw9Rule(string fixtureFileName, string expectedRuleId, string expectedField) {
        IDw4ValidatorInvoker invoker = Dw4ValidatorInvokerFactory.Create();

        Dw4ValidationOutcome outcome = invoker.Validate([
            Path.Combine(Dw4FixtureCatalog.FixtureRoot, fixtureFileName),
        ]);

        outcome.ExitCode.ShouldNotBe(0);
        Dw4Diagnostic diagnostic = outcome.Diagnostics.Single(d => d.Rule == expectedRuleId);
        diagnostic.Section.ShouldBe("Controls");
        diagnostic.Field.ShouldBe(expectedField);
        _ = diagnostic.Line.ShouldNotBeNull();
        diagnostic.Hint.ShouldNotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData("query-valid-linked-control-run.md")]
    [InlineData("signalr-valid-linked-control-run.md")]
    public void LinkedControlRunFixtures_Pass(string fixtureFileName) {
        IDw4ValidatorInvoker invoker = Dw4ValidatorInvokerFactory.Create();

        Dw4ValidationOutcome outcome = invoker.Validate([
            Path.Combine(Dw4FixtureCatalog.FixtureRoot, fixtureFileName),
        ]);

        outcome.ExitCode.ShouldBe(0);
        outcome.Diagnostics.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("skip-template.md", "template-pattern")]
    [InlineData("skip-marker-optout.md", "marker")]
    public void SkippedFiles_ReturnInformationalDiagnosticWithoutFailing(string fixtureFileName, string expectedReason) {
        IDw4ValidatorInvoker invoker = Dw4ValidatorInvokerFactory.Create();

        Dw4ValidationOutcome outcome = invoker.Validate([
            Path.Combine(Dw4FixtureCatalog.FixtureRoot, fixtureFileName),
        ]);

        outcome.ExitCode.ShouldBe(0);
        Dw4Diagnostic diagnostic = outcome.Diagnostics.Single();
        diagnostic.Rule.ShouldBe("evidence-file-skipped");
        diagnostic.Hint.ShouldContain(expectedReason);
    }

    [Fact]
    public void TemplateLookingFileWithoutSkipPredicate_IsStillAudited() {
        IDw4ValidatorInvoker invoker = Dw4ValidatorInvokerFactory.Create();

        Dw4ValidationOutcome outcome = invoker.Validate([
            Path.Combine(Dw4FixtureCatalog.FixtureRoot, "template-looking-invalid.md"),
        ]);

        outcome.ExitCode.ShouldNotBe(0);
        outcome.EmittedRuleIds.ShouldContain(Dw4RuleVocabulary.ControlLinkageMissing);
        outcome.EmittedRuleIds.ShouldNotContain("evidence-file-skipped");
    }
}
