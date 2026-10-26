namespace Hexalith.EventStore.Contracts.Routing;

/// <summary>Rejects invalid catalog inputs without including their values or parser exceptions.</summary>
public sealed class RouteCatalogValidationException : Exception
{
    /// <summary>Creates a support-safe failure.</summary>
    /// <param name="diagnostics">Bounded structural failure diagnostics.</param>
    public RouteCatalogValidationException(IReadOnlyList<RouteCatalogDiagnostic> diagnostics)
        : base("Route catalog rejected: " + string.Join("; ", diagnostics.Take(64).Select(d => $"{d.Path}: {d.Code}")))
    {
        Diagnostics = diagnostics.Take(64).ToArray();
    }

    /// <summary>Gets the safe field-level failures.</summary>
    public IReadOnlyList<RouteCatalogDiagnostic> Diagnostics { get; }
}
