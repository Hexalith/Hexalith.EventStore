
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hexalith.EventStore.Contracts.Projections;

/// <summary>
/// Response from a domain service's /project endpoint.
/// State is opaque — EventStore stores and serves it without interpreting the schema.
/// </summary>
/// <param name="ProjectionType">The projection type name (e.g., "counter-summary").</param>
/// <param name="State">The opaque projection state as a JSON element.
/// Callers must use <see cref="JsonElement.Clone"/> or keep the originating
/// <see cref="JsonDocument"/> alive — a disposed document invalidates this element.</param>
public record ProjectionResponse(
    string ProjectionType,
    JsonElement State)
{
    /// <summary>Gets optional sub-tenant notification scope; this filters a group and confers no authorization.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? NotificationGroupScope { get; init; }

    /// <summary>
    /// Gets optional opaque hint metadata, bounded before persistence and publication. Values must contain
    /// no domain payload, credential, ETag, cursor or authoritative completion/freshness evidence.
    /// Null preserves the legacy signal-only response; an empty map explicitly requests detail.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string>? NotificationMetadata { get; init; }
}
