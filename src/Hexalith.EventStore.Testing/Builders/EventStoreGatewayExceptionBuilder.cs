using System.Text.Json;

using Hexalith.EventStore.Client.Gateway;

namespace Hexalith.EventStore.Testing.Builders;

/// <summary>
/// Fluent builder for deterministic EventStore gateway exceptions in downstream tests.
/// </summary>
public sealed class EventStoreGatewayExceptionBuilder {
    private int _statusCode;
    private string _title;
    private string? _type;
    private string? _detail;
    private string? _correlationId;
    private string? _tenantId;
    private Dictionary<string, string>? _errors;
    private string? _reason;
    private string? _retryAfter;
    private Dictionary<string, JsonElement>? _extensions;

    private EventStoreGatewayExceptionBuilder(int statusCode, string title) {
        _statusCode = statusCode;
        _title = title;
    }

    /// <summary>
    /// Creates a validation failure builder.
    /// </summary>
    public static EventStoreGatewayExceptionBuilder Validation(
        string correlationId,
        string tenantId,
        IReadOnlyDictionary<string, string> errors)
        => new(400, "Validation Failed") {
            _correlationId = correlationId,
            _tenantId = tenantId,
            _errors = new Dictionary<string, string>(errors, StringComparer.Ordinal),
            _reason = "validation-failed",
        };

    /// <summary>
    /// Creates an authentication failure builder.
    /// </summary>
    public static EventStoreGatewayExceptionBuilder AuthenticationRequired(string correlationId)
        => new(401, "Unauthorized") {
            _correlationId = correlationId,
            _reason = "authentication-required",
        };

    /// <summary>
    /// Creates an authorization failure builder.
    /// </summary>
    public static EventStoreGatewayExceptionBuilder AuthorizationDenied(string correlationId, string tenantId)
        => new(403, "Forbidden") {
            _correlationId = correlationId,
            _tenantId = tenantId,
            _reason = "authorization-denied",
        };

    /// <summary>
    /// Creates a conflict failure builder.
    /// </summary>
    public static EventStoreGatewayExceptionBuilder Conflict(string correlationId, string tenantId)
        => new(409, "Conflict") {
            _correlationId = correlationId,
            _tenantId = tenantId,
            _reason = "conflict",
        };

    /// <summary>
    /// Creates a stale/degraded query failure builder.
    /// </summary>
    public static EventStoreGatewayExceptionBuilder Stale(string correlationId, string tenantId)
        => new(409, "Stale projection") {
            _correlationId = correlationId,
            _tenantId = tenantId,
            _reason = "stale",
        };

    /// <summary>
    /// Creates a service unavailable failure builder.
    /// </summary>
    public static EventStoreGatewayExceptionBuilder Unavailable(
        string correlationId,
        string tenantId,
        string? retryAfter = null)
        => new(503, "Service Unavailable") {
            _correlationId = correlationId,
            _tenantId = tenantId,
            _reason = "unavailable",
            _retryAfter = retryAfter,
        };

    /// <summary>
    /// Creates an unexpected gateway failure builder.
    /// </summary>
    public static EventStoreGatewayExceptionBuilder Unexpected(string correlationId)
        => new(500, "Internal Server Error") {
            _correlationId = correlationId,
            _reason = "unexpected",
        };

    /// <summary>
    /// Sets the problem type URI.
    /// </summary>
    public EventStoreGatewayExceptionBuilder WithType(string? type) { _type = type; return this; }

    /// <summary>
    /// Sets the problem detail.
    /// </summary>
    public EventStoreGatewayExceptionBuilder WithDetail(string? detail) { _detail = detail; return this; }

    /// <summary>
    /// Sets the reason extension.
    /// </summary>
    public EventStoreGatewayExceptionBuilder WithReason(string? reason) { _reason = reason; return this; }

    /// <summary>
    /// Sets the retry-after extension.
    /// </summary>
    public EventStoreGatewayExceptionBuilder WithRetryAfter(string? retryAfter) { _retryAfter = retryAfter; return this; }

    /// <summary>
    /// Adds a ProblemDetails extension.
    /// </summary>
    public EventStoreGatewayExceptionBuilder WithExtension(string name, JsonElement value) {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _extensions ??= new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        _extensions[name] = value.Clone();
        return this;
    }

    /// <summary>
    /// Builds the configured exception.
    /// </summary>
    public EventStoreGatewayException Build()
        => new(
            _statusCode,
            _title,
            _type,
            _detail,
            _correlationId,
            _tenantId,
            _errors,
            _reason,
            _retryAfter,
            _extensions);
}
