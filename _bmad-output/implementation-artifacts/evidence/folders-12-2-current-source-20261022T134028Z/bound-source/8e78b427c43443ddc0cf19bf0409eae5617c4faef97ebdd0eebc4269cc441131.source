using System.Text.Json;

using Hexalith.EventStore.Contracts.Queries;

namespace Hexalith.EventStore.Client.Gateway;

/// <summary>
/// Result returned by the EventStore query gateway client.
/// </summary>
/// <param name="CorrelationId">The correlation identifier for a 200 response.</param>
/// <param name="Payload">The query payload for a 200 response.</param>
/// <param name="IsNotModified">Whether the gateway returned HTTP 304 Not Modified.</param>
/// <param name="ETag">The response ETag header when present.</param>
public sealed record EventStoreQueryResult(
    string? CorrelationId,
    JsonElement? Payload,
    bool IsNotModified,
    string? ETag) {
    /// <summary>
    /// Gets response metadata supplied by the query gateway.
    /// </summary>
    public QueryResponseMetadata? Metadata { get; init; }
}

/// <summary>
/// Typed result returned by the EventStore query gateway client.
/// </summary>
/// <typeparam name="T">The expected query payload type.</typeparam>
/// <param name="CorrelationId">The correlation identifier for a 200 response.</param>
/// <param name="Payload">The typed query payload for a 200 response.</param>
/// <param name="IsNotModified">Whether the gateway returned HTTP 304 Not Modified.</param>
/// <param name="ETag">The response ETag header when present.</param>
public sealed record EventStoreQueryResult<T>(
    string? CorrelationId,
    T? Payload,
    bool IsNotModified,
    string? ETag) {
    /// <summary>
    /// Gets response metadata supplied by the query gateway.
    /// </summary>
    public QueryResponseMetadata? Metadata { get; init; }
}
