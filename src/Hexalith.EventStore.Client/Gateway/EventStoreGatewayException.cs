namespace Hexalith.EventStore.Client.Gateway;

/// <summary>
/// Exception thrown when the EventStore gateway returns a non-successful response.
/// </summary>
public sealed class EventStoreGatewayException : Exception {
    /// <summary>
    /// Initializes a new instance of the <see cref="EventStoreGatewayException"/> class.
    /// </summary>
    public EventStoreGatewayException(
        int statusCode,
        string title,
        string? type = null,
        string? detail = null,
        string? correlationId = null)
        : base(detail ?? title) {
        StatusCode = statusCode;
        Title = title;
        Type = type;
        Detail = detail;
        CorrelationId = correlationId;
    }

    /// <summary>
    /// Gets the HTTP status code.
    /// </summary>
    public int StatusCode { get; }

    /// <summary>
    /// Gets the problem details title or HTTP reason phrase.
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// Gets the RFC 7807 problem type URI when provided.
    /// </summary>
    public string? Type { get; }

    /// <summary>
    /// Gets the RFC 7807 problem detail when provided.
    /// </summary>
    public string? Detail { get; }

    /// <summary>
    /// Gets the correlation identifier returned by the gateway when provided.
    /// </summary>
    public string? CorrelationId { get; }
}
