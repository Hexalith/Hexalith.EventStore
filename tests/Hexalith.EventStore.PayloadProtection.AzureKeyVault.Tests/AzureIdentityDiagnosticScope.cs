namespace Hexalith.EventStore.PayloadProtection.AzureKeyVault.Conformance;

/// <summary>Attaches independently configured identity selection to observations; never changes vendor listeners or payloads.</summary>
internal sealed class AzureIdentityDiagnosticScope : IDisposable
{
    private static readonly AsyncLocal<string?> Selected = new();
    private readonly string? _previous;

    /// <summary>Enrolls the configured identity only for this execution context.</summary>
    internal AzureIdentityDiagnosticScope(string? selectedClientId)
    {
        _previous = Selected.Value;
        Selected.Value = selectedClientId;
    }

    /// <summary>The actual selection at the instant an observation is captured.</summary>
    internal static string? Current => Selected.Value;

    /// <inheritdoc/>
    public void Dispose() => Selected.Value = _previous;
}
