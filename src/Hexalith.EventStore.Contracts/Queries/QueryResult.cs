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
// Namespace pinned to the original Server.Actors CLR namespace so DataContractSerializer
// wire documents remain compatible when callers and callees redeploy independently.
[DataContract(Namespace = "http://schemas.datacontract.org/2004/07/Hexalith.EventStore.Server.Actors")]
public record QueryResult(
    [property: DataMember] bool Success,
    [property: DataMember] byte[]? PayloadBytes = null,
    [property: DataMember] string? ErrorMessage = null,
    [property: DataMember] string? ProjectionType = null) {
    /// <summary>
    /// Deserializes <see cref="PayloadBytes"/> to a <see cref="JsonElement"/>.
    /// </summary>
    /// <returns>
    /// The JSON payload, or <c>default</c> (<see cref="JsonValueKind.Undefined"/>) when
    /// <see cref="PayloadBytes"/> is null or empty. Callers must check
    /// <c>result.ValueKind != JsonValueKind.Undefined</c> before accessing properties.
    /// </returns>
    public JsonElement GetPayload()
        => PayloadBytes is { Length: > 0 }
            ? JsonSerializer.Deserialize<JsonElement>(PayloadBytes)
            : default;

    /// <summary>
    /// Creates a successful query result from a JSON payload.
    /// </summary>
    /// <param name="payload">The JSON payload to serialize as UTF-8 bytes. Must not be <see cref="JsonValueKind.Undefined"/>.</param>
    /// <param name="projectionType">Optional projection type metadata.</param>
    /// <returns>A successful query result.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="payload"/> has <see cref="JsonValueKind.Undefined"/>.</exception>
    public static QueryResult FromPayload(JsonElement payload, string? projectionType = null) {
        if (payload.ValueKind == JsonValueKind.Undefined) {
            throw new ArgumentException("Payload element must not be Undefined.", nameof(payload));
        }

        return new(true, JsonSerializer.SerializeToUtf8Bytes(payload), ProjectionType: projectionType);
    }

    /// <summary>
    /// Creates an unsuccessful query result with a coarse adapter-edge error.
    /// </summary>
    /// <param name="errorMessage">The failure message or <see cref="QueryAdapterFailureReason"/> category. Must not be null or whitespace.</param>
    /// <returns>An unsuccessful query result.</returns>
    public static QueryResult Failure(string errorMessage) {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);
        return new(false, ErrorMessage: errorMessage);
    }
}
