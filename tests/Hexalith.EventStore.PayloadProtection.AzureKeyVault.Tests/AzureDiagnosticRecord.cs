using System.Text.Json;

namespace Hexalith.EventStore.PayloadProtection.AzureKeyVault.Conformance;

/// <summary>Retains observer provenance and every payload field in memory without rendering raw capture in assertions.</summary>
/// <param name="Channel">The actual observer channel.</param>
/// <param name="Source">The actual event source.</param>
/// <param name="Name">The actual event name.</param>
/// <param name="EventId">The vendor event identifier, when present.</param>
/// <param name="Payload">Every observed payload field.</param>
/// <param name="SelectedClientId">The independently selected credential identity in the execution scope.</param>
internal sealed record AzureDiagnosticRecord(string Channel, string Source, string Name, int? EventId,
    IReadOnlyList<string> Payload, string? SelectedClientId)
{
    /// <summary>Complete observation text used only for private in-memory scans.</summary>
    internal string Content => string.Join("|", new[] { Channel, Source, Name, EventId?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "" }.Concat(Payload));

    /// <summary>A closed capture failure marker; unreadable observations can never count as safe.</summary>
    internal const string UnreadablePayload = "AZURE_DIAGNOSTIC_PAYLOAD_UNREADABLE";

    /// <summary>Captures every field or retains an affirmative failure marker instead of dropping an observation.</summary>
    internal static string Capture(object? value)
    {
        try
        {
            if (value is string text) return text;
            return (value?.ToString() ?? "") + "\n" + JsonSerializer.Serialize(value);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException) { return UnreadablePayload; }
    }

    /// <inheritdoc/>
    public override string ToString() => nameof(AzureDiagnosticRecord);
}
