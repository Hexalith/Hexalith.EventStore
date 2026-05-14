using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hexalith.EventStore.Contracts.Queries;

/// <summary>
/// Represents the response from a submitted query.
/// </summary>
public record SubmitQueryResponse {
    /// <summary>
    /// Initializes a new instance of the <see cref="SubmitQueryResponse"/> class.
    /// </summary>
    /// <param name="CorrelationId">The correlation identifier linking this response to its request.</param>
    /// <param name="Payload">The JSON payload containing the query result. Only meaningful when <paramref name="Success"/> is <c>true</c>.</param>
    /// <param name="Success">
    /// Indicates whether the upstream query pipeline executed successfully. When <c>false</c>, callers
    /// MUST classify the failure from <paramref name="ErrorMessage"/> before attempting to deserialize
    /// <paramref name="Payload"/> because payload contents are undefined for failed envelopes.
    /// </param>
    /// <param name="ErrorMessage">
    /// Optional semantic failure message when <paramref name="Success"/> is <c>false</c>
    /// (e.g. <c>"Forbidden"</c>, <c>"Tenant not found"</c>). Distinct from transport/HTTP-level failures.
    /// </param>
    /// <param name="Metadata">Optional public cache, freshness, paging, and warning metadata.</param>
    [JsonConstructor]
    public SubmitQueryResponse(
        string CorrelationId,
        JsonElement Payload,
        bool Success = true,
        string? ErrorMessage = null,
        QueryResponseMetadata? Metadata = null) {
        this.CorrelationId = CorrelationId;
        this.Payload = Payload;
        this.Success = Success;
        this.ErrorMessage = ErrorMessage;
        this.Metadata = Metadata;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SubmitQueryResponse"/> class
    /// using the original successful two-field wire contract.
    /// </summary>
    public SubmitQueryResponse(string correlationId, JsonElement payload)
        : this(correlationId, payload, true, null) {
    }

    /// <summary>
    /// Gets the correlation identifier linking this response to its request.
    /// </summary>
    public string CorrelationId { get; init; }

    /// <summary>
    /// Gets the JSON payload containing the query result. Only meaningful when <see cref="Success"/> is <c>true</c>.
    /// </summary>
    public JsonElement Payload { get; init; }

    /// <summary>
    /// Gets a value indicating whether the upstream query pipeline executed successfully.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Gets the optional semantic failure message when <see cref="Success"/> is <c>false</c>.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Gets optional public cache, freshness, paging, and warning metadata.
    /// </summary>
    public QueryResponseMetadata? Metadata { get; init; }

    /// <summary>
    /// Supports source compatibility for callers deconstructing the original two-field contract.
    /// </summary>
    public void Deconstruct(out string correlationId, out JsonElement payload) {
        correlationId = CorrelationId;
        payload = Payload;
    }

    /// <summary>
    /// Supports deconstruction of the full query response contract.
    /// </summary>
    public void Deconstruct(
        out string correlationId,
        out JsonElement payload,
        out bool success,
        out string? errorMessage) {
        correlationId = CorrelationId;
        payload = Payload;
        success = Success;
        errorMessage = ErrorMessage;
    }
}
