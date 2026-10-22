using System.Text.Json;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

/// <summary>Safely captures Dapr state error bodies without hiding malformed infrastructure output.</summary>
internal static class DaprStateErrorParser
{
    /// <summary>Parsed fields plus a non-throwing parse diagnostic.</summary>
    /// <param name="ErrorCode">The Dapr error code when present as a string.</param>
    /// <param name="Message">The Dapr error message when present as a string.</param>
    /// <param name="ParseError">Why exact fields could not be captured, or null for a complete body.</param>
    internal sealed record Capture(string? ErrorCode, string? Message, string? ParseError);

    /// <summary>Parses a response body without throwing for empty, non-JSON, or partial objects.</summary>
    /// <param name="responseBody">The verbatim HTTP response body.</param>
    /// <returns>The captured fields and parse diagnostic.</returns>
    public static Capture Parse(string responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return new Capture(null, null, "response body was empty");
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(responseBody);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return new Capture(null, null, "response body root was not a JSON object");
            }

            string? errorCode = TryGetString(document.RootElement, "errorCode");
            string? message = TryGetString(document.RootElement, "message");
            var missing = new List<string>(2);
            if (errorCode is null)
            {
                missing.Add("errorCode");
            }

            if (message is null)
            {
                missing.Add("message");
            }

            return new Capture(
                errorCode,
                message,
                missing.Count == 0 ? null : $"missing string field(s): {string.Join(", ", missing)}");
        }
        catch (JsonException ex)
        {
            return new Capture(null, null, $"invalid JSON: {ex.Message}");
        }
    }

    private static string? TryGetString(JsonElement root, string propertyName)
        => root.TryGetProperty(propertyName, out JsonElement value)
            && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
}
