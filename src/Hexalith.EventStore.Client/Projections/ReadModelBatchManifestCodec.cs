using System.Text.Json;

using Hexalith.EventStore.Contracts.Projections;

namespace Hexalith.EventStore.Client.Projections;

/// <summary>Restores only the original canonical manifest, preserving all identity and concurrency bytes.</summary>
internal static class ReadModelBatchManifestCodec
{
    internal static ReadModelBatch Decode(ReadOnlyMemory<byte> manifest, ReadModelBatchScope expectedScope,
        ProjectionOwnerSelection selection, string expectedFingerprint)
        => Decode(manifest, expectedScope, selection, null, expectedFingerprint);

    /// <summary>Restores the same coordinated replay manifest and its fingerprint-bound owner selections.</summary>
    internal static ReadModelBatch Decode(ReadOnlyMemory<byte> manifest, ReadModelBatchScope expectedScope,
        ProjectionOwnerSelection? selection, IReadOnlyList<ProjectionOwnerSelection>? selections, string expectedFingerprint)
    {
        if (manifest.Length > 1_048_576 || ReadModelBatchFingerprint.ComputeFromManifest(manifest.Span) != expectedFingerprint)
            throw new ArgumentException("The original immutable manifest is unavailable.");
        using JsonDocument document = JsonDocument.Parse(manifest);
        JsonElement root = document.RootElement;
        if (root.GetProperty("v").GetInt32() != 1) throw new ArgumentException("Unsupported manifest.");
        JsonElement source = root.GetProperty("scope");
        var scope = new ReadModelBatchScope(source.GetProperty("store").GetString()!, source.GetProperty("tenant").GetString()!,
            source.GetProperty("domain").GetString()!, source.GetProperty("aggregate").GetString()!,
            source.GetProperty("projection").GetString()!, source.GetProperty("batch").GetString()!);
        if (scope != expectedScope) throw new ArgumentException("The original manifest scope changed.");
        var operations = new List<ReadModelBatchOperation>();
        foreach (JsonElement value in root.GetProperty("ops").EnumerateArray())
        {
            if (value.GetProperty("ord").GetInt32() != operations.Count)
                throw new ArgumentException("The original manifest order changed.");
            string key = value.GetProperty("key").GetString()!;
            if (!Enum.TryParse(value.GetProperty("cmode").GetString(), out ReadModelBatchConcurrencyMode mode))
                throw new ArgumentException("Invalid original manifest concurrency.");
            var concurrency = new ReadModelBatchConcurrency(mode, value.GetProperty("etag").GetString()!);
            string kind = value.GetProperty("kind").GetString()!;
            ReadModelBatchOperation operation = kind switch
            {
                "w" => ReadModelBatchOperation.WriteCanonical(key, value.GetProperty("type").GetString()!,
                    value.GetProperty("val").GetBytesFromBase64(), value.TryGetProperty("ttl", out JsonElement ttl)
                        ? TimeSpan.FromSeconds(ttl.GetInt64()) : null).WithConcurrency(concurrency),
                "d" => ReadModelBatchOperation.Delete(key, concurrency),
                _ => throw new ArgumentException("Invalid original manifest operation."),
            };
            operations.Add(operation);
        }
        var result = new ReadModelBatch(scope, operations) { OwnerSelection = selection, OwnerSelections = selections };
        if (!ReadModelBatchFingerprint.BuildCanonicalManifest(result).AsSpan().SequenceEqual(manifest.Span)
            || ReadModelOwnerSelectionMetadata.FromBatch(result) != selection
            || !(ReadModelOwnerSelectionMetadata.FromCoordinatedBatch(result) ?? []).SequenceEqual(selections ?? []))
            throw new ArgumentException("The original manifest is not canonical.");
        return result;
    }
}
