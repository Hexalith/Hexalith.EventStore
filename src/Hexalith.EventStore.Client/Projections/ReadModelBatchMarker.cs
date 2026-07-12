using System.Text.Json.Serialization;

namespace Hexalith.EventStore.Client.Projections;

/// <summary>
/// The durable marker/receipt record for a coordinated batch. It is the visibility decision for the
/// resumable protocol and the terminal completion evidence for both profiles. Active/prepared/aborting
/// markers carry the operation list needed for reconciliation; the terminal completed receipt retains only
/// the scope hash, batch identity, fingerprint, terminal time, and protocol version.
/// </summary>
internal sealed class ReadModelBatchMarker {
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
