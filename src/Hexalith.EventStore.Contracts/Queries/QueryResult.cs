using System.Runtime.Serialization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hexalith.EventStore.Contracts.Queries;

/// <summary>
/// Result returned by a projection actor's query method.
/// </summary>
/// <remarks>
/// <see cref="PayloadBytes"/> is byte-based because <see cref="JsonElement"/>
/// is not a stable <see cref="DataContractSerializer"/> payload for DAPR
/// actor remoting. Future members must be additive to preserve actor wire
/// compatibility. The projection actor proxy invokes this type over the
/// System.Text.Json (weak) actor path, so exactly one constructor is annotated
/// with <see cref="JsonConstructorAttribute"/> to keep deserialization unambiguous
/// (a second unannotated public constructor makes System.Text.Json throw).
/// </remarks>
// Namespace pinned to the original Server.Actors CLR namespace so DataContractSerializer
// wire documents remain compatible when callers and callees redeploy independently.
[DataContract(Namespace = "http://schemas.datacontract.org/2004/07/Hexalith.EventStore.Server.Actors")]
public record QueryResult {
    /// <summary>
    /// Initializes a new instance of the <see cref="QueryResult"/> record.
    /// </summary>
    /// <param name="success">Whether the projection actor served the query successfully.</param>
    /// <param name="payloadBytes">UTF-8 JSON payload bytes for successful responses.</param>
    /// <param name="errorMessage">Coarse adapter-edge failure text for unsuccessful responses.</param>
    /// <param name="projectionType">Optional projection type metadata used by EventStore cache/ETag behavior.</param>
    /// <param name="metadata">Optional public cache, freshness, paging, and warning metadata.</param>
    [JsonConstructor]
    public QueryResult(
        bool success,
        byte[]? payloadBytes = null,
        string? errorMessage = null,
        string? projectionType = null,
        QueryResponseMetadata? metadata = null) {
        Success = success;
        PayloadBytes = payloadBytes;
        ErrorMessage = errorMessage;
        ProjectionType = projectionType;
        Metadata = metadata;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryResult"/> record using the original actor response shape.
    /// </summary>
    /// <param name="success">Whether the projection actor served the query successfully.</param>
    /// <param name="payloadBytes">UTF-8 JSON payload bytes for successful responses.</param>
    /// <param name="errorMessage">Coarse adapter-edge failure text for unsuccessful responses.</param>
    /// <param name="projectionType">Optional projection type metadata used by EventStore cache/ETag behavior.</param>
    public QueryResult(bool success, byte[]? payloadBytes, string? errorMessage, string? projectionType)
        : this(success, payloadBytes, errorMessage, projectionType, metadata: null) {
    }

    /// <summary>Gets a value indicating whether the projection actor served the query successfully.</summary>
    [DataMember]
    public bool Success { get; init; }

    /// <summary>Gets the UTF-8 JSON payload bytes for successful responses.</summary>
    [DataMember]
    public byte[]? PayloadBytes { get; init; }

    /// <summary>Gets the coarse adapter-edge failure text for unsuccessful responses.</summary>
    [DataMember]
    public string? ErrorMessage { get; init; }

    /// <summary>Gets the optional projection type metadata used by EventStore cache/ETag behavior.</summary>
    [DataMember]
    public string? ProjectionType { get; init; }

    /// <summary>Gets the optional public cache, freshness, paging, and warning metadata.</summary>
    [DataMember]
    public QueryResponseMetadata? Metadata { get; init; }

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
    public static QueryResult FromPayload(JsonElement payload, string? projectionType = null)
        => FromPayload(payload, projectionType, metadata: null);

    /// <summary>
    /// Creates a successful query result from a JSON payload.
    /// </summary>
    /// <param name="payload">The JSON payload to serialize as UTF-8 bytes. Must not be <see cref="JsonValueKind.Undefined"/>.</param>
    /// <param name="projectionType">Optional projection type metadata.</param>
    /// <param name="metadata">Optional public cache, freshness, paging, and warning metadata.</param>
    /// <returns>A successful query result.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="payload"/> has <see cref="JsonValueKind.Undefined"/>.</exception>
    public static QueryResult FromPayload(JsonElement payload, string? projectionType, QueryResponseMetadata? metadata) {
        if (payload.ValueKind == JsonValueKind.Undefined) {
            throw new ArgumentException("Payload element must not be Undefined.", nameof(payload));
        }

        return new(true, JsonSerializer.SerializeToUtf8Bytes(payload), projectionType: projectionType, metadata: metadata);
    }

    /// <summary>
    /// Creates an unsuccessful query result with a coarse adapter-edge error.
    /// </summary>
    /// <param name="errorMessage">The failure message or <see cref="QueryAdapterFailureReason"/> category. Must not be null or whitespace.</param>
    /// <returns>An unsuccessful query result.</returns>
    public static QueryResult Failure(string errorMessage)
        => Failure(errorMessage, metadata: null);

    /// <summary>
    /// Creates an unsuccessful query result with a coarse adapter-edge error.
    /// </summary>
    /// <param name="errorMessage">The failure message or <see cref="QueryAdapterFailureReason"/> category. Must not be null or whitespace.</param>
    /// <param name="metadata">Optional public cache, freshness, paging, and warning metadata.</param>
    /// <returns>An unsuccessful query result.</returns>
    public static QueryResult Failure(string errorMessage, QueryResponseMetadata? metadata) {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);
        return new(false, errorMessage: errorMessage, metadata: metadata);
    }
}
