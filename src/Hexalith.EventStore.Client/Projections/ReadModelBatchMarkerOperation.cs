using System.Text.Json.Serialization;

namespace Hexalith.EventStore.Client.Projections;

/// <summary>
/// The minimal per-operation descriptor retained in a prepared/committed marker so reconciliation and
/// compaction can locate every affected key without re-reading the caller's manifest. Compacted away when
/// the marker shrinks to its terminal receipt.
/// </summary>
internal sealed class ReadModelBatchMarkerOperation {
    /// <summary>Gets or sets the ordinal position.</summary>
    [JsonPropertyName("ord")]
    public int Ordinal { get; set; }

    /// <summary>Gets or sets the logical key.</summary>
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether the operation is a delete.</summary>
    [JsonPropertyName("del")]
    public bool IsDelete { get; set; }
}
