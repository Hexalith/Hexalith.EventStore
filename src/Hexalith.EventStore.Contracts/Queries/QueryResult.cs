using System.Runtime.Serialization;
using System.Text.Json;

namespace Hexalith.EventStore.Contracts.Queries;

/// <summary>
/// Result returned by a projection actor's query method.
/// </summary>
/// <param name="Success">Whether the projection actor served the query successfully.</param>
/// <param name="PayloadBytes">UTF-8 JSON payload bytes for successful responses.</param>
/// <param name="ErrorMessage">Coarse adapter-edge failure text for unsuccessful responses.</param>
/// <param name="ProjectionType">Optional projection type metadata used by EventStore cache/ETag behavior.</param>
/// <remarks>
/// <see cref="PayloadBytes"/> is byte-based because <see cref="JsonElement"/>
/// is not a stable <see cref="DataContractSerializer"/> payload for DAPR
/// actor remoting. Future members must be additive to preserve actor wire
/// compatibility.
/// </remarks>
[DataContract]
public record QueryResult(
    [property: DataMember] bool Success,
    [property: DataMember] byte[]? PayloadBytes = null,
    [property: DataMember] string? ErrorMessage = null,
    [property: DataMember] string? ProjectionType = null) {
    /// <summary>
    /// Deserializes <see cref="PayloadBytes"/> to a <see cref="JsonElement"/>.
    /// </summary>
    /// <returns>The JSON payload, or default when no payload bytes are present.</returns>
    public JsonElement GetPayload()
        => PayloadBytes is { Length: > 0 }
            ? JsonSerializer.Deserialize<JsonElement>(PayloadBytes)
            : default;

    /// <summary>
    /// Creates a successful query result from a JSON payload.
    /// </summary>
    /// <param name="payload">The JSON payload to serialize as UTF-8 bytes.</param>
    /// <param name="projectionType">Optional projection type metadata.</param>
    /// <returns>A successful query result.</returns>
    public static QueryResult FromPayload(JsonElement payload, string? projectionType = null)
        => new(true, JsonSerializer.SerializeToUtf8Bytes(payload), ProjectionType: projectionType);

    /// <summary>
    /// Creates an unsuccessful query result with a coarse adapter-edge error.
    /// </summary>
    /// <param name="errorMessage">The failure message or category.</param>
    /// <returns>An unsuccessful query result.</returns>
    public static QueryResult Failure(string errorMessage)
        => new(false, ErrorMessage: errorMessage);
}
