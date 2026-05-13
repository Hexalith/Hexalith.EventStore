using System.Text.Json;

namespace Hexalith.EventStore.Client.Gateway;

/// <summary>
/// Exception thrown when the EventStore gateway returns a non-successful response.
/// </summary>
public sealed class EventStoreGatewayException : Exception {
    private static readonly IReadOnlyDictionary<string, string> EmptyErrors = new Dictionary<string, string>();
    private static readonly IReadOnlyDictionary<string, JsonElement> EmptyExtensions = new Dictionary<string, JsonElement>();

    /// <summary>
    /// Initializes a new instance of the <see cref="EventStoreGatewayException"/> class.
    /// </summary>
    public EventStoreGatewayException(
        int statusCode,
        string title,
        string? type = null,
        string? detail = null,
        string? correlationId = null,
        string? tenantId = null,
        IReadOnlyDictionary<string, string>? errors = null,
        string? reason = null,
        string? retryAfter = null,
        IReadOnlyDictionary<string, JsonElement>? extensions = null)
        : base(detail ?? title) {
        StatusCode = statusCode;
        Title = title;
        Type = type;
        Detail = detail;
        CorrelationId = correlationId;
        TenantId = tenantId;
        Errors = errors is null ? EmptyErrors : new Dictionary<string, string>(errors, StringComparer.Ordinal);
        Reason = reason;
        RetryAfter = retryAfter;
        Extensions = extensions is null ? EmptyExtensions : new Dictionary<string, JsonElement>(extensions, StringComparer.Ordinal);
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

    /// <summary>
    /// Gets the tenant identifier returned by the gateway when provided.
    /// </summary>
    public string? TenantId { get; }

    /// <summary>
    /// Gets validation errors returned by the gateway.
    /// </summary>
    public IReadOnlyDictionary<string, string> Errors { get; }

    /// <summary>
    /// Gets the reason code or reason text returned by the gateway when provided.
    /// </summary>
    public string? Reason { get; }

    /// <summary>
    /// Gets retry guidance returned by the gateway when provided.
    /// </summary>
    public string? RetryAfter { get; }

    /// <summary>
    /// Gets non-standard ProblemDetails extensions returned by the gateway.
    /// </summary>
    public IReadOnlyDictionary<string, JsonElement> Extensions { get; }
}
