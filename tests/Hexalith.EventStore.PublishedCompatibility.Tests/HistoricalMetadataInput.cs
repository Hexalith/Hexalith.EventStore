using System.Text.Json;

namespace Hexalith.EventStore.PublishedCompatibility.Tests;

/// <summary>Retains only finite numeric metadata-case inputs, without copying raw probe stdin or credentials.</summary>
/// <param name="Head">The actual supplied current sequence.</param>
/// <param name="Floor">The supplied retained floor, or its genuine legacy absence.</param>
/// <param name="Pascal">Whether the producer used its explicit Pascal-case legacy variant.</param>
internal sealed record HistoricalMetadataInput(long Head, long? Floor, bool Pascal, string ETagSha256, long TimestampUtcTicks)
{
    /// <summary>Extracts only the metadata producer's finite numeric coordinates from its actual in-memory request.</summary>
    internal static HistoricalMetadataInput? Capture(JsonElement input)
    {
        if (!input.TryGetProperty("value", out var value) || value.ValueKind != JsonValueKind.String) return null;
        using var document = JsonDocument.Parse(value.GetString()!);
        var metadata = document.RootElement;
        bool pascal = metadata.TryGetProperty("CurrentSequence", out var sequence);
        if (!pascal && !metadata.TryGetProperty("currentSequence", out sequence)) return null;
        long? floor = metadata.TryGetProperty("retainedFloor", out var retained) ? retained.GetInt64() : null;
        string etag = metadata.GetProperty(pascal ? "ETag" : "eTag").GetString()!;
        var timestamp = metadata.GetProperty(pascal ? "LastModified" : "lastModified").GetDateTimeOffset();
        return new(sequence.GetInt64(), floor, pascal,
            QualificationRuntime.Hash(System.Text.Encoding.UTF8.GetBytes(etag)), timestamp.UtcTicks);
    }
}
