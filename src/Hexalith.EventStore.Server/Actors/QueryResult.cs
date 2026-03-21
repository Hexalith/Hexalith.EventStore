using System.Runtime.Serialization;
using System.Text.Json;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>
/// Result returned by a projection actor's QueryAsync method.
/// The Payload is stored as <c>byte[]</c> (UTF-8 JSON) because <c>JsonElement</c>
/// is not serializable by <c>DataContractSerializer</c> used by DAPR actor remoting.
/// </summary>
/// <remarks>
/// [DataContract]/[DataMember] required for DAPR actor proxy serialization
/// (matches CommandProcessingResult pattern).
/// </remarks>
[DataContract]
public record QueryResult(
    [property: DataMember] bool Success,
    [property: DataMember] byte[]? PayloadBytes = null,
    [property: DataMember] string? ErrorMessage = null,
    [property: DataMember] string? ProjectionType = null) {
    /// <summary>
    /// Deserializes <see cref="PayloadBytes"/> to a <see cref="JsonElement"/>.
    /// Returns <c>default</c> if <see cref="PayloadBytes"/> is null or empty.
    /// </summary>
    public JsonElement GetPayload()
        => PayloadBytes is { Length: > 0 }
            ? JsonSerializer.Deserialize<JsonElement>(PayloadBytes)
            : default;

    /// <summary>
    /// Creates a successful <see cref="QueryResult"/> from a <see cref="JsonElement"/>.
    /// </summary>
    public static QueryResult FromPayload(JsonElement payload, string? projectionType = null)
        => new(true, JsonSerializer.SerializeToUtf8Bytes(payload), ProjectionType: projectionType);

    /// <summary>
    /// Creates a failure <see cref="QueryResult"/>.
    /// </summary>
    public static QueryResult Failure(string errorMessage)
        => new(false, ErrorMessage: errorMessage);
}
