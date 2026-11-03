namespace Hexalith.EventStore.PayloadProtection.AzureKeyVault.Tests;

/// <summary>Exercises a serialization getter failure without allowing its exception text into exported evidence.</summary>
internal sealed class ThrowingDiagnosticPayload(string text)
{
    /// <summary>The deliberately unreadable payload field.</summary>
    public string Value => throw new InvalidOperationException(text);
}
