
using System.Text.Json;

namespace Hexalith.EventStore.Contracts.Queries;

/// <summary>
/// Represents the response from a submitted query.
/// </summary>
/// <param name="CorrelationId">The correlation identifier linking this response to its request.</param>
/// <param name="Payload">The JSON payload containing the query result. Only meaningful when <paramref name="Success"/> is <c>true</c>.</param>
/// <param name="Success">
/// Indicates whether the upstream query pipeline executed successfully. When <c>false</c>, callers
/// MUST classify the failure from <paramref name="ErrorMessage"/> before attempting to deserialize
/// <paramref name="Payload"/> — payload contents are undefined for failed envelopes.
/// </param>
/// <param name="ErrorMessage">
/// Optional semantic failure message when <paramref name="Success"/> is <c>false</c>
/// (e.g. <c>"Forbidden"</c>, <c>"Tenant not found"</c>). Distinct from transport/HTTP-level failures.
/// </param>
public record SubmitQueryResponse(
    string CorrelationId,
    JsonElement Payload,
    bool Success = true,
    string? ErrorMessage = null);
