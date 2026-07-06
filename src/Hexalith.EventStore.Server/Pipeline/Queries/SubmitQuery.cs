
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
    bool IsGlobalAdmin = false,
    QueryPagingOptions? Paging = null) : IRequest<SubmitQueryResult> {
    /// <summary>
    /// Initializes a new instance of the <see cref="SubmitQuery"/> record using the original query shape.
    /// </summary>
    public SubmitQuery(
        string tenant,
        string domain,
        string aggregateId,
        string queryType,
        byte[] payload,
        string correlationId,
        string userId,
        string? entityId,
        string? projectionType,
        string? projectionActorType,
        bool isGlobalAdmin)
        : this(
            tenant,
            domain,
            aggregateId,
            queryType,
            payload,
            correlationId,
            userId,
            entityId,
            projectionType,
            projectionActorType,
            isGlobalAdmin,
            Paging: null) {
    }
}

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
