
using System.Text.Json;

using Hexalith.EventStore.Contracts.Queries;

namespace Hexalith.EventStore.Server.Queries;
/// <summary>
/// Result of routing a query to a projection actor.
/// Allows the router to signal "not found" without throwing.
/// </summary>
public record QueryRouterResult(
    bool Success,
    JsonElement? Payload,
    bool NotFound,
    string? ErrorMessage = null,
    string? ProjectionType = null,
    QueryResponseMetadata? Metadata = null) {
    /// <summary>
    /// Initializes a new instance of the <see cref="QueryRouterResult"/> class using the original router result shape.
    /// </summary>
    /// <param name="success">Whether routing succeeded.</param>
    /// <param name="payload">The routed JSON payload.</param>
    /// <param name="notFound">Whether the target projection/query was not found.</param>
    /// <param name="errorMessage">Optional failure detail.</param>
    /// <param name="projectionType">Optional projection type metadata.</param>
    public QueryRouterResult(
        bool success,
        JsonElement? payload,
        bool notFound,
        string? errorMessage,
        string? projectionType)
        : this(success, payload, notFound, errorMessage, projectionType, Metadata: null) {
    }
}
