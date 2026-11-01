using System.Text;
using System.Text.Json;

namespace Hexalith.EventStore.PublishedCompatibility.Tests;

/// <summary>Finite actual fixture mutations; values and payloads remain irreversible commitments.</summary>
internal sealed record HistoricalFixtureOperation(string Operation, string Key, string? ValueSha256,
    long? RetainedFloor, long? SourceProofRetainedFloor, long? MetadataVersion,
    string? SerializationFormat, string? EventTypeNameSha256, string? PayloadSha256)
{
    internal static HistoricalFixtureOperation? Capture(JsonElement item)
    {
        if (item.ValueKind != JsonValueKind.Object
            || !item.TryGetProperty("operation", out var operation) || operation.ValueKind != JsonValueKind.String
            || operation.GetString() is not ("upsert" or "delete")
            || !item.TryGetProperty("key", out var key) || key.ValueKind != JsonValueKind.String
            || key.GetString() is not { Length: > 0 and <= 512 } name
            || !name.All(character => char.IsAsciiLetterOrDigit(character) || character is ':' or '-' or '_')) return null;
        if (operation.GetString() == "delete")
            return item.TryGetProperty("value", out _) ? null : new("delete", name, null, null, null, null, null, null, null);
        if (!item.TryGetProperty("value", out var value) || value.ValueKind != JsonValueKind.Object) return null;
        foreach (string field in new[] { "retainedFloor", "metadataVersion" })
            if (value.TryGetProperty(field, out var number) && (number.ValueKind != JsonValueKind.Number || !number.TryGetInt64(out _))) return null;
        long? proofFloor = null;
        if (value.TryGetProperty("sourceProof", out var proof) && proof.ValueKind == JsonValueKind.Object
            && proof.TryGetProperty("retainedFloor", out var floor))
        {
            if (floor.ValueKind != JsonValueKind.Number || !floor.TryGetInt64(out long actual)) return null;
            proofFloor = actual;
        }
        string? format = value.TryGetProperty("serializationFormat", out var serializer)
            ? serializer.ValueKind == JsonValueKind.String && serializer.GetString() is "json" or "protected+json" ? serializer.GetString() : "unsupported" : null;
        return new("upsert", name, HistoricalWireInput.HashValue(value), Number(value, "retainedFloor"), proofFloor,
            Number(value, "metadataVersion"), format, Digest(value, "eventTypeName"), Digest(value, "payload"));
    }

    private static long? Number(JsonElement value, string key)
        => value.TryGetProperty(key, out var field) ? field.GetInt64() : null;

    private static string? Digest(JsonElement value, string key)
        => value.TryGetProperty(key, out var field) && field.ValueKind == JsonValueKind.String
            ? QualificationRuntime.Hash(Encoding.UTF8.GetBytes(field.GetString()!)) : null;
}
