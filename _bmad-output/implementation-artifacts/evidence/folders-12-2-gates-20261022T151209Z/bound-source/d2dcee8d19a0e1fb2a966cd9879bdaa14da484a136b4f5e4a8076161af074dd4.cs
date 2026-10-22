namespace Hexalith.EventStore.OperationalEvidence.Validator.Tests;

/// <summary>
/// Stable diagnostic shape pinned by AC #13. Every emitted diagnostic must
/// be projectable into this record. JSON output uses the same field names.
///
/// <see cref="Level"/> reflects the validator's `level` field added in DW9
/// alongside the informational `evidence-file-skipped` rule. Defaults to
/// <c>"error"</c> for diagnostics that pre-date the field.
/// </summary>
internal sealed record Dw4Diagnostic(
    string File,
    string? Schema,
    string Rule,
    string? Section,
    string? Field,
    int? Line,
    string Hint,
    string Level = "error");

/// <summary>
/// Aggregated outcome for a single fixture invocation: the run's exit/return
/// code plus emitted diagnostics in canonical order.
/// </summary>
internal sealed record Dw4ValidationOutcome(
    int ExitCode,
    IReadOnlyList<Dw4Diagnostic> Diagnostics) {

    public bool Passed => ExitCode == 0 && Diagnostics.Count == 0;

    public IReadOnlySet<string> EmittedRuleIds
        => Diagnostics.Select(d => d.Rule).ToHashSet(StringComparer.Ordinal);
}

/// <summary>
/// Stable comparer for diagnostics: <c>(File, Schema, Rule, Section, Field, Line)</c>
/// ascending. Used by <see cref="Dw4DiagnosticShapeAtddTests"/> to assert
/// deterministic ordering across runs.
/// </summary>
internal sealed class Dw4DiagnosticComparer : IComparer<Dw4Diagnostic> {
    public static readonly Dw4DiagnosticComparer Instance = new();

    public int Compare(Dw4Diagnostic? x, Dw4Diagnostic? y) {
        if (ReferenceEquals(x, y)) {
            return 0;
        }

        if (x is null) {
            return -1;
        }

        if (y is null) {
            return 1;
        }

        int c = string.CompareOrdinal(x.File, y.File);
        if (c != 0) {
            return c;
        }

        c = string.CompareOrdinal(x.Schema ?? string.Empty, y.Schema ?? string.Empty);
        if (c != 0) {
            return c;
        }

        c = string.CompareOrdinal(x.Rule, y.Rule);
        if (c != 0) {
            return c;
        }

        c = string.CompareOrdinal(x.Section ?? string.Empty, y.Section ?? string.Empty);
        if (c != 0) {
            return c;
        }

        c = string.CompareOrdinal(x.Field ?? string.Empty, y.Field ?? string.Empty);
        if (c != 0) {
            return c;
        }

        return Comparer<int?>.Default.Compare(x.Line, y.Line);
    }
}
