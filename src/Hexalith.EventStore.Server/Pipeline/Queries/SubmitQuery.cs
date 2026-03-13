
using System.Text.Json;

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
    string? EntityId = null) : IRequest<SubmitQueryResult>;

/// <summary>
/// Result of processing a <see cref="SubmitQuery"/>.
/// </summary>
public record SubmitQueryResult(string CorrelationId, JsonElement Payload);
