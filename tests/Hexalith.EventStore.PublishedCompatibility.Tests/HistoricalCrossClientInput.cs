using System.Text.Json;

namespace Hexalith.EventStore.PublishedCompatibility.Tests;

/// <summary>Actual finite destination coordinates, never caller authority or protected request contents.</summary>
internal sealed record HistoricalCrossClientInput(string Sidecar, string Actor)
{
    internal static HistoricalCrossClientInput? Capture(JsonElement input)
    {
        if (!input.TryGetProperty("sidecar", out var sidecar) || sidecar.ValueKind != JsonValueKind.String
            || !input.TryGetProperty("actor", out var actor) || actor.ValueKind != JsonValueKind.String) return null;
        string target = sidecar.GetString()!, id = actor.GetString()!; string[] parts = id.Split(':');
        if (!Uri.TryCreate(target, UriKind.Absolute, out var uri) || uri.Scheme != "http" || !uri.IsLoopback || uri.Port <= 0
            || uri.UserInfo.Length != 0 || uri.AbsolutePath != "/" || uri.Query.Length != 0 || uri.Fragment.Length != 0
            || id.Length > 256 || parts.Length != 3 || parts[1] != "counter"
            || parts.Any(part => part.Length == 0 || !part.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_'))) return null;
        return new(target, id);
    }
}
