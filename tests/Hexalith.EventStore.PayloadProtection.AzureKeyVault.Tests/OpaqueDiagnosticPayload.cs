namespace Hexalith.EventStore.PayloadProtection.AzureKeyVault.Tests;

/// <summary>Exercises textual payload data that has no public JSON properties.</summary>
internal sealed class OpaqueDiagnosticPayload(string text)
{
    /// <inheritdoc/>
    public override string ToString() => text;
}
