namespace Hexalith.EventStore.DeferredWorkGovernance.Tests;

/// <summary>
/// Stable diagnostic shape for the DW6 checker. The implementation may be a
/// script or .NET tool, but its JSON output should project into this record.
/// </summary>
internal sealed record Dw6GovernanceDiagnostic(
    string File,
    string Rule,
    string? Disposition,
    string Heading,
    string Excerpt,
    int? Line,
    string Hint);

internal sealed record Dw6GovernanceReport(
    int ExitCode,
    IReadOnlyDictionary<string, int> Counts,
    IReadOnlyList<Dw6GovernanceDiagnostic> Diagnostics) {

    public bool Passed => ExitCode == 0 && Diagnostics.Count == 0;

    public IReadOnlySet<string> EmittedRuleIds
        => Diagnostics.Select(d => d.Rule).ToHashSet(StringComparer.Ordinal);
}

internal sealed class Dw6GovernanceDiagnosticComparer : IComparer<Dw6GovernanceDiagnostic> {
    public static readonly Dw6GovernanceDiagnosticComparer Instance = new();

    public int Compare(Dw6GovernanceDiagnostic? x, Dw6GovernanceDiagnostic? y) {
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

        c = string.CompareOrdinal(x.Heading, y.Heading);
        if (c != 0) {
            return c;
        }

        c = string.CompareOrdinal(x.Rule, y.Rule);
        if (c != 0) {
            return c;
        }

        c = string.CompareOrdinal(x.Disposition ?? string.Empty, y.Disposition ?? string.Empty);
        if (c != 0) {
            return c;
        }

        return Comparer<int?>.Default.Compare(x.Line, y.Line);
    }
}
