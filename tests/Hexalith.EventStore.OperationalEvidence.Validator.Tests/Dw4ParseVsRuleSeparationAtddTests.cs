using Hexalith.EventStore.OperationalEvidence.Validator.Tests.Fixtures;

using Shouldly;

namespace Hexalith.EventStore.OperationalEvidence.Validator.Tests;

/// <summary>
/// DW4 ATDD red-phase scaffolds for AC #13 / Advanced Elicitation: parser
/// failures must produce parser-family rule ids ('parse-*'), distinct from
/// business-rule ids. A malformed-YAML fixture must NOT also emit
/// 'query-required-metadata-missing' — once parsing fails, business rules
/// haven't run yet, so claiming they did would falsely prove rule coverage.
/// </summary>
public class Dw4ParseVsRuleSeparationAtddTests {
    private const string _baseSkip = "ATDD red phase — DW4 ";

    [Fact(Skip = _baseSkip + "AC#13 — malformed YAML must produce parse-yaml-malformed without business-rule noise. Remove Skip when implementing.")]
    public void ParserFailure_MalformedYaml_ProducesParseFamilyOnly() => AssertParserFailureIsolated(
            "parse-malformed-yaml.md",
            Dw4RuleVocabulary.ParseYamlMalformed);

    [Fact(Skip = _baseSkip + "AC#13 — malformed table must produce parse-table-malformed without business-rule noise. Remove Skip when implementing.")]
    public void ParserFailure_MalformedTable_ProducesParseFamilyOnly() => AssertParserFailureIsolated(
            "parse-malformed-table.md",
            Dw4RuleVocabulary.ParseTableMalformed);

    [Fact(Skip = _baseSkip + "AC#13 — duplicate required heading must produce parse-heading-duplicate. Remove Skip when implementing.")]
    public void ParserFailure_DuplicateRequiredHeading_ProducesParseFamilyOnly() => AssertParserFailureIsolated(
            "parse-duplicate-required-heading.md",
            Dw4RuleVocabulary.ParseHeadingDuplicate);

    private static void AssertParserFailureIsolated(string fixtureFileName, string expectedParserRuleId) {
        Dw4FixtureCatalog.FixtureExpectation expected = Dw4FixtureCatalog.ByName[fixtureFileName];
        expected.ExpectedVerdict.ShouldBe(Dw4FixtureCatalog.Verdict.Fail);
        Dw4RuleVocabulary.ParserFamily.ShouldContain(expectedParserRuleId);

        IDw4ValidatorInvoker invoker = Dw4ValidatorInvokerFactory.Create();
        Dw4ValidationOutcome outcome = invoker.Validate([
            Path.Combine(Dw4FixtureCatalog.FixtureRoot, fixtureFileName),
        ]);

        outcome.Passed.ShouldBeFalse();
        outcome.EmittedRuleIds.ShouldContain(expectedParserRuleId);

        // Critical fail-closed contract: when parsing fails, business rules
        // (required-metadata, placeholder, classification, control, redaction,
        // not-applicable, profile-aspire) MUST NOT also be emitted. Doing so
        // would let a parser regression silently produce coverage-shaped
        // diagnostics for rules that never ran.
        IReadOnlySet<string> businessFamilies = new HashSet<string>(StringComparer.Ordinal) {
            Dw4RuleVocabulary.QueryRequiredMetadataMissing,
            Dw4RuleVocabulary.SignalrRequiredMetadataMissing,
            Dw4RuleVocabulary.RequiredTableCellEmpty,
            Dw4RuleVocabulary.PlaceholderUnreplaced,
            Dw4RuleVocabulary.ClassificationInvalid,
            Dw4RuleVocabulary.ControlRequiredMissing,
            Dw4RuleVocabulary.CorrelationControlRequiredMissing,
            Dw4RuleVocabulary.RedactionSectionMissing,
            Dw4RuleVocabulary.RedactionUnsafeBearerToken,
            Dw4RuleVocabulary.RedactionUnsafeConnectionString,
            Dw4RuleVocabulary.RedactionUnsafeProductionHostname,
            Dw4RuleVocabulary.RedactionRawSecretMarker,
            Dw4RuleVocabulary.NotApplicableReasonMissing,
            Dw4RuleVocabulary.NotApplicableNotAllowedHere,
            Dw4RuleVocabulary.ProfileAspireFieldsMissing,
        };

        IEnumerable<string> noise = outcome.EmittedRuleIds.Where(businessFamilies.Contains);
        noise.ShouldBeEmpty(
            "Parser failures must isolate from business rules. Emitting business-rule " +
            "diagnostics from a malformed input would falsely prove rule coverage.");
    }
}
