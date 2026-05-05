using Shouldly;

namespace Hexalith.EventStore.OperationalEvidence.Validator.Tests;

/// <summary>
/// DW4 ATDD red-phase scaffolds for AC #4 — classification taxonomy is
/// single-source. The validator's allowed-classification enum for query and
/// SignalR must match the templates literally; any drift between docs,
/// templates, and validator data must fail.
///
/// These tests exercise a documented test seam:
/// <c>OperationalEvidenceValidator.GetSupportedClassifications(schemaVersion)</c>
/// returning <see cref="IReadOnlySet{String}"/>. If the dev's chosen
/// implementation does not expose this seam directly, the dev must add an
/// equivalent (e.g. read the validator's rule data file from the test) and
/// adapt this test to the chosen accessor.
/// </summary>
public class Dw4TaxonomyMappingAtddTests {
    private const string _baseSkip = "ATDD red phase — DW4 ";

    [Fact(Skip = _baseSkip + "AC#4 — validator's query/v1 classification set matches the template enum. Remove Skip when implementing.")]
    public void TaxonomyMapping_QuerySchemaClassifications_MatchTemplate() {
        IReadOnlySet<string> validatorEnum = LoadValidatorClassificationsForSchema(Dw4RuleVocabulary.SchemaQueryV1);

        validatorEnum.ShouldBe(
            Dw4RuleVocabulary.QueryClassifications.ToHashSet(StringComparer.Ordinal),
            ignoreOrder: true,
            customMessage: "Validator's query/v1 classifications must match the 9 values declared in " +
                "_bmad-output/test-artifacts/query-operational-evidence-template.md. Drift here means " +
                "evidence files using a value the validator rejects (or accepts a value the docs forbid).");
    }

    [Fact(Skip = _baseSkip + "AC#4 — validator's signalr/v1 classification set matches the template enum. Remove Skip when implementing.")]
    public void TaxonomyMapping_SignalrSchemaClassifications_MatchTemplate() {
        IReadOnlySet<string> validatorEnum = LoadValidatorClassificationsForSchema(Dw4RuleVocabulary.SchemaSignalrV1);

        validatorEnum.ShouldBe(
            Dw4RuleVocabulary.SignalrClassifications.ToHashSet(StringComparer.Ordinal),
            ignoreOrder: true,
            customMessage: "Validator's signalr/v1 classifications must match the 6 values declared in " +
                "_bmad-output/test-artifacts/signalr-operational-evidence-template.md.");
    }

    [Fact(Skip = _baseSkip + "AC#4 — query and SignalR classification sets intentionally differ; mapping table is single-source. Remove Skip when implementing.")]
    public void TaxonomyMapping_QueryAndSignalrDifferencesAreDocumented() {
        // The query schema has 9 classifications; SignalR has 6. The story
        // explicitly says: "If wording still differs intentionally between
        // query and SignalR, record the difference in one visible mapping table
        // rather than leaving duplicate reviewer checklists to drift silently."
        // This test asserts the validator exposes a mapping accessor, so docs
        // and validator stay in lock-step.
        IReadOnlyDictionary<string, IReadOnlyList<string>> mapping = LoadValidatorTaxonomyMapping();

        mapping.ContainsKey(Dw4RuleVocabulary.SchemaQueryV1).ShouldBeTrue();
        mapping.ContainsKey(Dw4RuleVocabulary.SchemaSignalrV1).ShouldBeTrue();

        IReadOnlyList<string> queryValues = mapping[Dw4RuleVocabulary.SchemaQueryV1];
        IReadOnlyList<string> signalrValues = mapping[Dw4RuleVocabulary.SchemaSignalrV1];

        // Query-only classifications (not in SignalR enum) must include at least
        // the four query-specific downgrade values: path-viability, diagnostic-only,
        // not-claimable, instrumentation-gap is shared. The exact diff is asserted
        // narrowly to detect drift.
        IEnumerable<string> queryOnly = queryValues.Except(signalrValues);
        queryOnly.ShouldContain("path-viability");
        queryOnly.ShouldContain("diagnostic-only");
        queryOnly.ShouldContain("not-claimable");
    }

    /// <summary>
    /// Test seam: load the validator's allowed-classification set for the given
    /// schema. Until the validator is wired, this throws to keep the red phase
    /// honest. Dev adapts this method to the chosen validator's accessor:
    /// reflection over a static class, parsing a rule-data JSON/YAML file, or
    /// reading from <c>OperationalEvidenceValidator.GetSupportedClassifications</c>.
    /// </summary>
    private static IReadOnlySet<string> LoadValidatorClassificationsForSchema(string schemaVersion)
        => throw new Dw4ValidatorNotConfiguredException(
            $"DW4 taxonomy seam not wired for '{schemaVersion}'. Wire " +
            "Dw4TaxonomyMappingAtddTests.LoadValidatorClassificationsForSchema to the chosen " +
            "validator's classification accessor (reflection, JSON file, or static API).");

    /// <summary>
    /// Test seam: load the full taxonomy mapping table the validator exposes.
    /// Dev replaces with the chosen validator's accessor.
    /// </summary>
    private static IReadOnlyDictionary<string, IReadOnlyList<string>> LoadValidatorTaxonomyMapping()
        => throw new Dw4ValidatorNotConfiguredException(
            "DW4 taxonomy mapping seam not wired. Provide a static accessor returning the " +
            "validator's per-schema classification list so docs and validator stay in lock-step.");
}
