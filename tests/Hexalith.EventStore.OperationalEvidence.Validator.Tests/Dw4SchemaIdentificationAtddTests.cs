using Hexalith.EventStore.OperationalEvidence.Validator.Tests.Fixtures;

using Shouldly;

namespace Hexalith.EventStore.OperationalEvidence.Validator.Tests;

/// <summary>
/// DW4 ATDD red-phase scaffolds for schema identification (AC #1, #13 / Advanced Elicitation).
/// The validator must fail closed BEFORE any rule evaluates when the schema
/// version is missing, duplicated, contradictory, or unsupported. Schema-id
/// failures must use rule ids prefixed <c>schema-version-</c> (separate family
/// from business rules).
/// </summary>
public class Dw4SchemaIdentificationAtddTests {
    private const string _baseSkip = "ATDD red phase — DW4 ";

    [Fact(Skip = _baseSkip + "AC#1 / Adv-Elicitation — file with no schema marker must fail with schema-version-missing. Remove Skip when implementing.")]
    public void SchemaIdentification_NoSchemaMarker_FailsClosedBeforeRules() {
        AssertSchemaIdFailure(
            "schema-missing.md",
            Dw4RuleVocabulary.SchemaVersionMissing);
    }

    [Fact(Skip = _baseSkip + "AC#1 / Adv-Elicitation — file with duplicate schema markers must fail with schema-version-duplicate. Remove Skip when implementing.")]
    public void SchemaIdentification_DuplicateSchemaMarkers_FailsClosedBeforeRules() {
        AssertSchemaIdFailure(
            "schema-duplicate-markers.md",
            Dw4RuleVocabulary.SchemaVersionDuplicate);
    }

    [Fact(Skip = _baseSkip + "AC#1 / Adv-Elicitation — file with contradictory schema markers must fail with schema-version-contradictory. Remove Skip when implementing.")]
    public void SchemaIdentification_ContradictorySchemaMarkers_FailsClosedBeforeRules() {
        AssertSchemaIdFailure(
            "schema-contradictory.md",
            Dw4RuleVocabulary.SchemaVersionContradictory);
    }

    [Fact(Skip = _baseSkip + "AC#1 — unsupported future schema version must fail with schema-version-unsupported. Remove Skip when implementing.")]
    public void SchemaIdentification_UnsupportedFutureVersion_FailsClosed() {
        AssertSchemaIdFailure(
            "schema-unsupported-future-version.md",
            Dw4RuleVocabulary.SchemaVersionUnsupported);
    }

    private static void AssertSchemaIdFailure(string fixtureFileName, string expectedRuleId) {
        Dw4FixtureCatalog.FixtureExpectation expected = Dw4FixtureCatalog.ByName[fixtureFileName];
        expected.ExpectedVerdict.ShouldBe(Dw4FixtureCatalog.Verdict.Fail);

        IDw4ValidatorInvoker invoker = Dw4ValidatorInvokerFactory.Create();
        Dw4ValidationOutcome outcome = invoker.Validate([
            Path.Combine(Dw4FixtureCatalog.FixtureRoot, fixtureFileName),
        ]);

        outcome.Passed.ShouldBeFalse();
        outcome.EmittedRuleIds.ShouldContain(expectedRuleId);

        // Fail-closed: when the schema cannot be identified, business-rule
        // diagnostics MUST NOT also be emitted (otherwise a duplicate schema
        // marker could falsely produce 'placeholder-unreplaced' noise that
        // masks the real schema-id problem).
        IEnumerable<string> businessRuleNoise = outcome.EmittedRuleIds
            .Where(r => !Dw4RuleVocabulary.SchemaIdentificationFamily.Contains(r)
                     && !Dw4RuleVocabulary.ParserFamily.Contains(r));
        businessRuleNoise.ShouldBeEmpty(
            "Schema-id failures must fail closed BEFORE business-rule evaluation. " +
            "Emitting business-rule diagnostics here would let an unidentified-schema " +
            "file mask its real defect with rule noise.");
    }
}
