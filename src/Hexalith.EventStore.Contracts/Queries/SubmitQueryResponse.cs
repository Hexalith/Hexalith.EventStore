
using System.Text.Json;

namespace Hexalith.EventStore.Contracts.Queries;

/// <summary>
/// Represents the response from a submitted query.
/// </summary>
/// <param name="CorrelationId">The correlation identifier linking this response to its request.</param>
/// <param name="Payload">The JSON payload containing the query result.</param>
public record SubmitQueryResponse(
    string CorrelationId,
    JsonElement Payload);
