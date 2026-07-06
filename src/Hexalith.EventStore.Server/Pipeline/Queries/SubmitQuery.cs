
using System.Text.Json;

using Hexalith.EventStore.Contracts.Queries;

using MediatR;

namespace Hexalith.EventStore.Server.Pipeline.Queries;
/// <summary>
/// MediatR request for submitting a query through the pipeline.
/// </summary>
public record SubmitQuery(
    string Tenant,
    string Domain,
    string AggregateId,
    string QueryType,
    byte[] Payload,
    string CorrelationId,
    string UserId,
    string? EntityId = null,
    string? ProjectionType = null,
    string? ProjectionActorType = null,
    bool IsGlobalAdmin = false) : IRequest<SubmitQueryResult>;

/// <summary>
/// Result of processing a <see cref="SubmitQuery"/>.
/// </summary>
public record SubmitQueryResult(
    string CorrelationId,
    JsonElement Payload,
    string? ProjectionType = null,
    QueryResponseMetadata? Metadata = null) {
    /// <summary>
    /// Initializes a new instance of the <see cref="SubmitQueryResult"/> class using the original query result shape.
    /// </summary>
    /// <param name="correlationId">The correlation identifier for the query.</param>
    /// <param name="payload">The query payload.</param>
    /// <param name="projectionType">Optional projection type metadata.</param>
    public SubmitQueryResult(string correlationId, JsonElement payload, string? projectionType)
        : this(correlationId, payload, projectionType, Metadata: null) {
    }
}
