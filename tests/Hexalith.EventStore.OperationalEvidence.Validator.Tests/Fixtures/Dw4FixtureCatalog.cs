namespace Hexalith.EventStore.OperationalEvidence.Validator.Tests.Fixtures;

/// <summary>
/// Single source of truth for the DW4 fixture coverage matrix.
/// Each entry pairs a fixture filename with its expected validator outcome
/// and (for negative fixtures) the rule ids that MUST be present in the
/// emitted diagnostics. Per AC #8 / Advanced Elicitation, asserting the exact
/// rule ids prevents an unrelated parser failure or new bug from masquerading
/// as rule coverage.
/// </summary>
internal static class Dw4FixtureCatalog {
    public const string FixtureRoot
        = "_bmad-output/test-artifacts/operational-evidence-validator/fixtures";

    public sealed record FixtureExpectation(
        string FileName,
        string SchemaSlot,
        Verdict ExpectedVerdict,
        IReadOnlySet<string> ExpectedRuleIds,
        string Notes);

    public enum Verdict {
        Pass,
        Fail,
    }

    public static readonly IReadOnlyList<FixtureExpectation> All = [
        // ---------- Query positives ----------
        new("query-valid-minimal.md",
            SchemaSlot: Dw4RuleVocabulary.SchemaQueryV1,
            Verdict.Pass,
            ExpectedRuleIds: new HashSet<string>(StringComparer.Ordinal),
            Notes: "Minimal valid query/v1 evidence with all required metadata, controls, redaction."),
        new("query-valid-not-applicable-aspire.md",
            SchemaSlot: Dw4RuleVocabulary.SchemaQueryV1,
            Verdict.Pass,
            ExpectedRuleIds: new HashSet<string>(StringComparer.Ordinal),
            Notes: "Valid query/v1 with Aspire fields marked 'not-applicable: non-aspire-proof'."),

        // ---------- Query negatives — required-field family ----------
        new("query-invalid-missing-metadata.md",
            SchemaSlot: Dw4RuleVocabulary.SchemaQueryV1,
            Verdict.Fail,
            ExpectedRuleIds: new HashSet<string>(StringComparer.Ordinal) {
                Dw4RuleVocabulary.QueryRequiredMetadataMissing,
            },
            Notes: "Missing 'evidence_run_id' from required metadata YAML block."),

        // ---------- Query negatives — placeholder family ----------
        new("query-invalid-placeholder-unreplaced.md",
            SchemaSlot: Dw4RuleVocabulary.SchemaQueryV1,
            Verdict.Fail,
            ExpectedRuleIds: new HashSet<string>(StringComparer.Ordinal) {
                Dw4RuleVocabulary.PlaceholderUnreplaced,
            },
            Notes: "Required field still contains '<required>' angle-bracket placeholder."),
        new("query-invalid-empty-required-table-cell.md",
            SchemaSlot: Dw4RuleVocabulary.SchemaQueryV1,
            Verdict.Fail,
            ExpectedRuleIds: new HashSet<string>(StringComparer.Ordinal) {
                Dw4RuleVocabulary.RequiredTableCellEmpty,
            },
            Notes: "Cache-State Setup table row left with empty required cells."),

        // ---------- Query negatives — classification family ----------
        new("query-invalid-classification-not-in-enum.md",
            SchemaSlot: Dw4RuleVocabulary.SchemaQueryV1,
            Verdict.Fail,
            ExpectedRuleIds: new HashSet<string>(StringComparer.Ordinal) {
                Dw4RuleVocabulary.ClassificationInvalid,
            },
            Notes: "final_classification: 'tbd' — not in the 9-value query/v1 enum."),

        // ---------- Query negatives — controls family ----------
        new("query-invalid-control-missing.md",
            SchemaSlot: Dw4RuleVocabulary.SchemaQueryV1,
            Verdict.Fail,
            ExpectedRuleIds: new HashSet<string>(StringComparer.Ordinal) {
                Dw4RuleVocabulary.ControlRequiredMissing,
            },
            Notes: "False-positive control block omitted from Controls section."),
        new("query-invalid-correlation-control-missing.md",
            SchemaSlot: Dw4RuleVocabulary.SchemaQueryV1,
            Verdict.Fail,
            ExpectedRuleIds: new HashSet<string>(StringComparer.Ordinal) {
                Dw4RuleVocabulary.CorrelationControlRequiredMissing,
            },
            Notes: "Correlation-integrity control block omitted from Controls section."),

        // ---------- Query negatives — redaction family ----------
        new("query-invalid-redaction-bearer-token.md",
            SchemaSlot: Dw4RuleVocabulary.SchemaQueryV1,
            Verdict.Fail,
            ExpectedRuleIds: new HashSet<string>(StringComparer.Ordinal) {
                Dw4RuleVocabulary.RedactionUnsafeBearerToken,
            },
            Notes: "JWT-shaped bearer token (eyJ...) left in diagnostics text."),
        new("query-invalid-redaction-connection-string.md",
            SchemaSlot: Dw4RuleVocabulary.SchemaQueryV1,
            Verdict.Fail,
            ExpectedRuleIds: new HashSet<string>(StringComparer.Ordinal) {
                Dw4RuleVocabulary.RedactionUnsafeConnectionString,
            },
            Notes: "Connection-string keyword 'Server=...; Password=...' present unredacted."),
        new("query-invalid-redaction-production-hostname.md",
            SchemaSlot: Dw4RuleVocabulary.SchemaQueryV1,
            Verdict.Fail,
            ExpectedRuleIds: new HashSet<string>(StringComparer.Ordinal) {
                Dw4RuleVocabulary.RedactionUnsafeProductionHostname,
            },
            Notes: "Customer production hostname '*.prod.contoso.com' left in evidence."),
        new("query-invalid-redaction-section-missing.md",
            SchemaSlot: Dw4RuleVocabulary.SchemaQueryV1,
            Verdict.Fail,
            ExpectedRuleIds: new HashSet<string>(StringComparer.Ordinal) {
                Dw4RuleVocabulary.RedactionSectionMissing,
            },
            Notes: "Redaction section heading omitted entirely."),
        new("query-invalid-raw-secret-marker.md",
            SchemaSlot: Dw4RuleVocabulary.SchemaQueryV1,
            Verdict.Fail,
            ExpectedRuleIds: new HashSet<string>(StringComparer.Ordinal) {
                Dw4RuleVocabulary.RedactionRawSecretMarker,
            },
            Notes: "AKIA-shaped synthetic AWS access key embedded in raw-sample artifact path."),

        // ---------- Query negatives — not-applicable marker family ----------
        new("query-invalid-not-applicable-empty-reason.md",
            SchemaSlot: Dw4RuleVocabulary.SchemaQueryV1,
            Verdict.Fail,
            ExpectedRuleIds: new HashSet<string>(StringComparer.Ordinal) {
                Dw4RuleVocabulary.NotApplicableReasonMissing,
            },
            Notes: "Field annotated 'not-applicable:' with empty/blank reason."),
        new("query-invalid-not-applicable-on-required-field.md",
            SchemaSlot: Dw4RuleVocabulary.SchemaQueryV1,
            Verdict.Fail,
            ExpectedRuleIds: new HashSet<string>(StringComparer.Ordinal) {
                Dw4RuleVocabulary.NotApplicableNotAllowedHere,
            },
            Notes: "'not-applicable: <reason>' used on a non-profile-specific required field (e.g., schema_version)."),

        // ---------- Query negatives — profile-scoped Aspire family ----------
        new("query-invalid-aspire-claimed-but-fields-missing.md",
            SchemaSlot: Dw4RuleVocabulary.SchemaQueryV1,
            Verdict.Fail,
            ExpectedRuleIds: new HashSet<string>(StringComparer.Ordinal) {
                Dw4RuleVocabulary.ProfileAspireFieldsMissing,
            },
            Notes: "Evidence claims Aspire/DAPR runtime proof but AppHost/placement fields blank."),

        // ---------- SignalR positives ----------
        new("signalr-valid-minimal.md",
            SchemaSlot: Dw4RuleVocabulary.SchemaSignalrV1,
            Verdict.Pass,
            ExpectedRuleIds: new HashSet<string>(StringComparer.Ordinal),
            Notes: "Minimal valid signalr/v1 evidence with all required metadata + controls + redaction."),

        // ---------- SignalR negatives ----------
        new("signalr-invalid-missing-metadata.md",
            SchemaSlot: Dw4RuleVocabulary.SchemaSignalrV1,
            Verdict.Fail,
            ExpectedRuleIds: new HashSet<string>(StringComparer.Ordinal) {
                Dw4RuleVocabulary.SignalrRequiredMetadataMissing,
            },
            Notes: "Missing 'Evidence run id' from Run Identity required fields."),
        new("signalr-invalid-placeholder-unreplaced.md",
            SchemaSlot: Dw4RuleVocabulary.SchemaSignalrV1,
            Verdict.Fail,
            ExpectedRuleIds: new HashSet<string>(StringComparer.Ordinal) {
                Dw4RuleVocabulary.PlaceholderUnreplaced,
            },
            Notes: "'<required>' angle-bracket placeholder left in Story/proof key."),
        new("signalr-invalid-classification-not-in-enum.md",
            SchemaSlot: Dw4RuleVocabulary.SchemaSignalrV1,
            Verdict.Fail,
            ExpectedRuleIds: new HashSet<string>(StringComparer.Ordinal) {
                Dw4RuleVocabulary.ClassificationInvalid,
            },
            Notes: "Classification: 'partial' — not in the 6-value signalr/v1 enum."),
        new("signalr-invalid-control-missing.md",
            SchemaSlot: Dw4RuleVocabulary.SchemaSignalrV1,
            Verdict.Fail,
            ExpectedRuleIds: new HashSet<string>(StringComparer.Ordinal) {
                Dw4RuleVocabulary.ControlRequiredMissing,
            },
            Notes: "Reliability Controls section has no Control N block."),
        new("signalr-invalid-redaction-bearer-token.md",
            SchemaSlot: Dw4RuleVocabulary.SchemaSignalrV1,
            Verdict.Fail,
            ExpectedRuleIds: new HashSet<string>(StringComparer.Ordinal) {
                Dw4RuleVocabulary.RedactionUnsafeBearerToken,
            },
            Notes: "JWT-shaped bearer token in Authenticated Join logs."),

        // ---------- Schema identification family ----------
        new("schema-missing.md",
            SchemaSlot: "(none)",
            Verdict.Fail,
            ExpectedRuleIds: new HashSet<string>(StringComparer.Ordinal) {
                Dw4RuleVocabulary.SchemaVersionMissing,
            },
            Notes: "Markdown file with no 'schema_version' / 'Schema version:' marker."),
        new("schema-duplicate-markers.md",
            SchemaSlot: "(both)",
            Verdict.Fail,
            ExpectedRuleIds: new HashSet<string>(StringComparer.Ordinal) {
                Dw4RuleVocabulary.SchemaVersionDuplicate,
            },
            Notes: "File declares both query/v1 and signalr/v1 schema markers."),
        new("schema-contradictory.md",
            SchemaSlot: "(conflict)",
            Verdict.Fail,
            ExpectedRuleIds: new HashSet<string>(StringComparer.Ordinal) {
                Dw4RuleVocabulary.SchemaVersionContradictory,
            },
            Notes: "Heading says signalr/v1, YAML metadata says query-operational-evidence/v1."),
        new("schema-unsupported-future-version.md",
            SchemaSlot: "query/v2",
            Verdict.Fail,
            ExpectedRuleIds: new HashSet<string>(StringComparer.Ordinal) {
                Dw4RuleVocabulary.SchemaVersionUnsupported,
            },
            Notes: "Declares query-operational-evidence/v2 — fail-closed until story extends mapping."),

        // ---------- Parser family (distinct from rule family per AC #13) ----------
        new("parse-malformed-yaml.md",
            SchemaSlot: Dw4RuleVocabulary.SchemaQueryV1,
            Verdict.Fail,
            ExpectedRuleIds: new HashSet<string>(StringComparer.Ordinal) {
                Dw4RuleVocabulary.ParseYamlMalformed,
            },
            Notes: "Required YAML metadata block has unbalanced quotes."),
        new("parse-malformed-table.md",
            SchemaSlot: Dw4RuleVocabulary.SchemaQueryV1,
            Verdict.Fail,
            ExpectedRuleIds: new HashSet<string>(StringComparer.Ordinal) {
                Dw4RuleVocabulary.ParseTableMalformed,
            },
            Notes: "Cache-State Setup table has mismatched pipe count between header and data rows."),
        new("parse-duplicate-required-heading.md",
            SchemaSlot: Dw4RuleVocabulary.SchemaQueryV1,
            Verdict.Fail,
            ExpectedRuleIds: new HashSet<string>(StringComparer.Ordinal) {
                Dw4RuleVocabulary.ParseHeadingDuplicate,
            },
            Notes: "'### Run Identity' heading appears twice — section identity ambiguous."),
    ];

    public static readonly IReadOnlyDictionary<string, FixtureExpectation> ByName
        = All.ToDictionary(f => f.FileName, StringComparer.Ordinal);

    /// <summary>
    /// All distinct rule ids exercised across negative fixtures. The
    /// fixture-coverage-matrix test asserts this set equals the validator's
    /// non-pass rule vocabulary so no rule family can be added to the
    /// vocabulary without a covering fixture.
    /// </summary>
    public static readonly IReadOnlySet<string> CoveredRuleIds
        = All.SelectMany(f => f.ExpectedRuleIds).ToHashSet(StringComparer.Ordinal);
}
