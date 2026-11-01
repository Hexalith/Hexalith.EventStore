using System.Text.Json;

namespace Hexalith.EventStore.PublishedCompatibility.Tests;

/// <summary>Safe actual wire-input commitments; no raw query proof or serialized input is retained.</summary>
internal sealed record HistoricalWireInput(string ValueSha256, IReadOnlyDictionary<string, string>? Fields)
{
    internal static HistoricalWireInput? Capture(JsonElement input)
    {
        if (!input.TryGetProperty("value", out var supplied) || supplied.ValueKind != JsonValueKind.String) return null;
        string value = supplied.GetString()!;
        IReadOnlyDictionary<string, string>? fields = null;
        try
        {
            using var parsed = JsonDocument.Parse(value);
            if (parsed.RootElement.ValueKind == JsonValueKind.Object) fields = HashFields(parsed.RootElement);
        }
        catch (JsonException) { }
        return new(QualificationRuntime.Hash(System.Text.Encoding.UTF8.GetBytes(value)), fields);
    }

    internal static IReadOnlyDictionary<string, string> HashFields(JsonElement value)
        => value.EnumerateObject().ToDictionary(property => property.Name, property => HashValue(property.Value), StringComparer.Ordinal);

    internal static string HashValue(JsonElement value)
    {
        using var bytes = new MemoryStream();
        using (var writer = new Utf8JsonWriter(bytes)) Write(writer, value);
        return QualificationRuntime.Hash(bytes.ToArray());
    }

    private static void Write(Utf8JsonWriter writer, JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            writer.WriteStartObject();
            foreach (var property in value.EnumerateObject().OrderBy(property => property.Name, StringComparer.Ordinal))
            { writer.WritePropertyName(property.Name); Write(writer, property.Value); }
            writer.WriteEndObject();
        }
        else if (value.ValueKind == JsonValueKind.Array)
        { writer.WriteStartArray(); foreach (var item in value.EnumerateArray()) Write(writer, item); writer.WriteEndArray(); }
        else if (value.ValueKind == JsonValueKind.String) writer.WriteStringValue(value.GetString());
        else if (value.ValueKind == JsonValueKind.Number) writer.WriteNumberValue(value.GetDecimal());
        else value.WriteTo(writer);
    }
}
