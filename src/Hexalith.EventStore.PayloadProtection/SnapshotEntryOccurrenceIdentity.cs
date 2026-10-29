using System.Text.Json.Serialization;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>Exact existing entry identity serialization, retaining the lowercase path field.</summary>
/// <param name="PayloadTypeId">Parent snapshot type.</param><param name="Path">Canonical protected value path.</param>
internal sealed record SnapshotEntryOccurrenceIdentity(string PayloadTypeId, [property: JsonPropertyName("path")] string Path);
