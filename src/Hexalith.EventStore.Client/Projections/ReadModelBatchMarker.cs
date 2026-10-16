using System.Text.Json.Serialization;
using System.Text.Json;

namespace Hexalith.EventStore.Client.Projections;

/// <summary>
/// The durable marker/receipt record for a coordinated batch. It is the visibility decision for the
/// resumable protocol and the terminal completion evidence for both profiles. Active/prepared/aborting
/// markers carry the operation list needed for reconciliation; the terminal completed receipt retains only
/// the scope hash, batch identity, fingerprint, terminal time, and protocol version.
/// </summary>
internal sealed class ReadModelBatchMarker {
    /// <summary>Reads bounded receipt evidence while rejecting duplicate fields throughout its owner metadata.</summary>
    internal static ReadModelBatchMarker? ReadProof(ReadOnlyMemory<byte> bytes)
    {
        using JsonDocument document = JsonDocument.Parse(bytes);
        return document.RootElement.ValueKind == JsonValueKind.Object && !HasDuplicateProperties(document.RootElement)
            ? document.RootElement.Deserialize<ReadModelBatchMarker>(JsonSerializerOptions.Web) : null;
    }

    private static bool HasDuplicateProperties(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (JsonProperty property in value.EnumerateObject())
                if (!names.Add(property.Name) || HasDuplicateProperties(property.Value)) return true;
        }
        else if (value.ValueKind == JsonValueKind.Array)
            foreach (JsonElement item in value.EnumerateArray())
                if (HasDuplicateProperties(item)) return true;
        return false;
    }

    /// <summary>The original fingerprint-bound manifest for explicitly opted-in owner recovery, absent on terminal receipts.</summary>
    [JsonPropertyName("manifest")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public byte[]? OriginalManifest { get; set; }
    /// <summary>The existing batch's immutable manifest key when its body requires the separate manifest byte budget.</summary>
    [JsonPropertyName("manifestKey")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? OriginalManifestKey { get; set; }
    /// <summary>Gets or sets optional owner evidence derived from the fingerprint-bound model value.</summary>
    [JsonPropertyName("owner")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Hexalith.EventStore.Contracts.Projections.ProjectionOwnerSelection? OwnerSelection { get; set; }
    /// <summary>Gets or sets optional selections from the same coordinated rebuild manifest.</summary>
    [JsonPropertyName("owners")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<Hexalith.EventStore.Contracts.Projections.ProjectionOwnerSelection>? OwnerSelections { get; set; }
    /// <summary>Gets or sets the marker record version.</summary>
    [JsonPropertyName("v")]
    public int Version { get; set; } = 1;

    /// <summary>Gets or sets the opaque scope hash.</summary>
    [JsonPropertyName("scope")]
    public string ScopeHash { get; set; } = string.Empty;

    /// <summary>Gets or sets the caller-supplied batch identity.</summary>
    [JsonPropertyName("batch")]
    public string BatchId { get; set; } = string.Empty;

    /// <summary>Gets or sets the versioned canonical fingerprint.</summary>
    [JsonPropertyName("fp")]
    public string Fingerprint { get; set; } = string.Empty;

    /// <summary>Gets or sets the durable status.</summary>
    [JsonPropertyName("st")]
    public ReadModelBatchMarkerStatus Status { get; set; }

    /// <summary>Gets or sets the operation descriptors (null on a terminal receipt).</summary>
    [JsonPropertyName("ops")]
    public IReadOnlyList<ReadModelBatchMarkerOperation>? Operations { get; set; }

    /// <summary>Gets or sets the terminal time (ISO-8601 UTC), set only on the completed receipt.</summary>
    [JsonPropertyName("time")]
    public string? TerminalTimeUtc { get; set; }
}
