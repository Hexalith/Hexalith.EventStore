namespace Hexalith.EventStore.OperationalEvidence.Validator.Tests;

/// <summary>
/// Frozen rule-id vocabulary that DW4's validator must emit. Tests assert
/// against these literals so dev cannot rename a rule without updating the
/// suite (intentional drift gate per AC #13).
///
/// Naming contract enforced by <see cref="Dw4DiagnosticShapeAtddTests"/>:
/// rule ids match <c>^[a-z][a-z0-9-]*$</c>, length ≤ 64.
/// Parser-family ids start with <c>parse-</c>.
/// Schema-id-family ids start with <c>schema-version-</c>.
/// </summary>
internal static class Dw4RuleVocabulary {
    public const string SchemaQueryV1 = "query-operational-evidence/v1";
    public const string SchemaSignalrV1 = "signalr-operational-evidence/v1";

    // --- Required-field family ----------------------------------------
    public const string QueryRequiredMetadataMissing = "query-required-metadata-missing";
    public const string SignalrRequiredMetadataMissing = "signalr-required-metadata-missing";
    public const string RequiredTableCellEmpty = "required-table-cell-empty";

    // --- Placeholder family -------------------------------------------
    public const string PlaceholderUnreplaced = "placeholder-unreplaced";

    // --- Classification family ----------------------------------------
    public const string ClassificationInvalid = "classification-invalid";

    // --- Control / correlation family ---------------------------------
    public const string ControlRequiredMissing = "control-required-missing";
    public const string CorrelationControlRequiredMissing = "correlation-control-required-missing";

    // --- Redaction family ---------------------------------------------
    public const string RedactionSectionMissing = "redaction-section-missing";
    public const string RedactionUnsafeBearerToken = "redaction-unsafe-bearer-token";
    public const string RedactionUnsafeConnectionString = "redaction-unsafe-connection-string";
    public const string RedactionUnsafeProductionHostname = "redaction-unsafe-production-hostname";
    public const string RedactionRawSecretMarker = "redaction-raw-secret-marker";

    // --- not-applicable marker family ---------------------------------
    public const string NotApplicableReasonMissing = "not-applicable-reason-missing";
    public const string NotApplicableNotAllowedHere = "not-applicable-not-allowed-here";

    // --- Profile-scoped family ----------------------------------------
    public const string ProfileAspireFieldsMissing = "profile-aspire-fields-missing";

    // --- Schema identification family (must start with "schema-version-") -
    public const string SchemaVersionMissing = "schema-version-missing";
    public const string SchemaVersionDuplicate = "schema-version-duplicate";
    public const string SchemaVersionContradictory = "schema-version-contradictory";
    public const string SchemaVersionUnsupported = "schema-version-unsupported";

    // --- Parser family (must start with "parse-") ---------------------
    public const string ParseYamlMalformed = "parse-yaml-malformed";
    public const string ParseTableMalformed = "parse-table-malformed";
    public const string ParseHeadingDuplicate = "parse-heading-duplicate";
    public const string ParseSectionAmbiguous = "parse-section-ambiguous";

    /// <summary>All rule ids the validator is committed to in DW4.</summary>
    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal) {
        QueryRequiredMetadataMissing,
        SignalrRequiredMetadataMissing,
        RequiredTableCellEmpty,
        PlaceholderUnreplaced,
        ClassificationInvalid,
        ControlRequiredMissing,
        CorrelationControlRequiredMissing,
        RedactionSectionMissing,
        RedactionUnsafeBearerToken,
        RedactionUnsafeConnectionString,
        RedactionUnsafeProductionHostname,
        RedactionRawSecretMarker,
        NotApplicableReasonMissing,
        NotApplicableNotAllowedHere,
        ProfileAspireFieldsMissing,
        SchemaVersionMissing,
        SchemaVersionDuplicate,
        SchemaVersionContradictory,
        SchemaVersionUnsupported,
        ParseYamlMalformed,
        ParseTableMalformed,
        ParseHeadingDuplicate,
        ParseSectionAmbiguous,
    };

    public static readonly IReadOnlySet<string> ParserFamily = new HashSet<string>(StringComparer.Ordinal) {
        ParseYamlMalformed,
        ParseTableMalformed,
        ParseHeadingDuplicate,
        ParseSectionAmbiguous,
    };

    public static readonly IReadOnlySet<string> SchemaIdentificationFamily = new HashSet<string>(StringComparer.Ordinal) {
        SchemaVersionMissing,
        SchemaVersionDuplicate,
        SchemaVersionContradictory,
        SchemaVersionUnsupported,
    };

    /// <summary>Allowed query/v1 run-level classifications, in template order.</summary>
    public static readonly IReadOnlyList<string> QueryClassifications = [
        "pass",
        "path-viability",
        "sample-only",
        "diagnostic-only",
        "not-claimable",
        "product-failure",
        "environment-blocker",
        "instrumentation-gap",
        "inconclusive",
    ];

    /// <summary>Allowed signalr/v1 run-level classifications, in template order.</summary>
    public static readonly IReadOnlyList<string> SignalrClassifications = [
        "pass",
        "product-failure",
        "environment-blocker",
        "instrumentation-gap",
        "sample-only",
        "inconclusive",
    ];

    /// <summary>Disposition markers required on DW4-relevant deferred-work bullets (AC #12).</summary>
    public static readonly IReadOnlySet<string> DispositionMarkers = new HashSet<string>(StringComparer.Ordinal) {
        "STORY:post-epic-deferred-dw4-operational-evidence-schema-validation",
        "RESOLVED",
        "ACCEPTED-DEBT",
        "DUPLICATE",
        "NO-ACTION",
    };
}
