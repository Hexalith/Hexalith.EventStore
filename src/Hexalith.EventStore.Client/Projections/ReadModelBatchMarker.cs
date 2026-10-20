using System.Globalization;
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
    /// <summary>The original logical control-record byte budget.</summary>
    internal const int MaxControlBytes = 16_384;

    /// <summary>Allows one bounded SDK anchor in addition to the unchanged logical control budget.</summary>
    internal const int MaxRecoveryEncodedBytes = MaxControlBytes
        + 6 * (6 * ReadModelBatchScope.MaxComponentByteLength + ReadModelBatchOptions.DefaultMaxKeyByteLength) + 4096;

    /// <summary>Reads bounded recovery evidence without charging SDK anchor expansion to the original control budget.</summary>
    internal static ReadModelBatchMarker? ReadBoundedProof(ReadOnlyMemory<byte> bytes)
    {
        if (bytes.Length > MaxRecoveryEncodedBytes) return null;
        ReadModelBatchMarker? marker = ReadProof(bytes);
        if (marker is null) return null;
        if (marker.RecoveryAnchor is { } anchor && (anchor.Scope is null || !anchor.Matches(marker, anchor.Scope))) return null;
        if (bytes.Length <= MaxControlBytes) return marker;
        if (marker.RecoveryAnchor is null) return null;
        // Preserve unknown control fields in the size check. Deserializing and reserializing only
        // known fields could silently admit an oversized original control record.
        using JsonDocument document = JsonDocument.Parse(bytes);
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            foreach (JsonProperty property in document.RootElement.EnumerateObject())
                if (!property.Name.Equals("recoveryAnchor", StringComparison.OrdinalIgnoreCase)
                    && !property.Name.Equals("retainedParticipantCount", StringComparison.OrdinalIgnoreCase))
                    property.WriteTo(writer);
            writer.WriteEndObject();
        }
        return buffer.Length <= MaxControlBytes ? marker : null;
    }

    /// <summary>Reads bounded receipt evidence while rejecting duplicate fields throughout its owner metadata.</summary>
    internal static ReadModelBatchMarker? ReadProof(ReadOnlyMemory<byte> bytes)
    {
        using JsonDocument document = JsonDocument.Parse(bytes);
        JsonElement root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object || HasDuplicateProperties(root)
            || !root.EnumerateObject().Any(static property => property.Name.Equals("v", StringComparison.OrdinalIgnoreCase))
            || !root.EnumerateObject().Any(static property => property.Name.Equals("st", StringComparison.OrdinalIgnoreCase)))
            return null;
        ReadModelBatchMarker? marker = root.Deserialize<ReadModelBatchMarker>(JsonSerializerOptions.Web);
        return marker is { Version: 1, RetainedParticipantCount: >= 0 and <= 100 } && Enum.IsDefined(marker.Status)
            && (marker.ScratchCompensationKey is null || marker.Status is ReadModelBatchMarkerStatus.Aborting or ReadModelBatchMarkerStatus.Aborted)
            && (marker.Status != ReadModelBatchMarkerStatus.Completed
            || IsUtcTerminalTime(marker.TerminalTimeUtc)) ? marker : null;
    }

    private static bool IsUtcTerminalTime(string? value)
        => value is not null && (value.EndsWith('Z') || value.EndsWith("+00:00", StringComparison.Ordinal))
            && DateTimeOffset.TryParseExact(value,
                ["yyyy-MM-dd'T'HH:mm:ss'Z'", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'",
                    "yyyy-MM-dd'T'HH:mm:sszzz", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFzzz"],
                CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset timestamp)
            && timestamp.Offset == TimeSpan.Zero;

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

    /// <summary>Immutable compensation evidence for a fully validated original legacy scratch batch.</summary>
    [JsonPropertyName("scratchCompensationKey")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ScratchCompensationKey { get; set; }

    /// <summary>Original retained participant count for crash-safe terminal retirement.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int RetainedParticipantCount { get; set; }

    /// <summary>Bounded terminal proof keeping the final existing envelope recoverable until manifest reclamation.</summary>
    [JsonPropertyName("recoveryAnchor")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ReadModelBatchRecoveryAnchor? RecoveryAnchor { get; set; }

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
