namespace Hexalith.EventStore.Contracts.Problems;

/// <summary>
/// Stable RFC 7807 ProblemDetails extension names used by EventStore gateway clients.
/// </summary>
public static class GatewayProblemDetailsExtensions {
    /// <summary>
    /// Extension name carrying the cross-system correlation identifier.
    /// </summary>
    public const string CorrelationId = "correlationId";

    /// <summary>
    /// Extension name carrying the tenant identifier associated with the problem.
    /// </summary>
    public const string TenantId = "tenantId";

    /// <summary>
    /// Extension name carrying validation errors as a dictionary from field name to message.
    /// </summary>
    public const string Errors = "errors";

    /// <summary>
    /// Extension name carrying a stable reason code or human-readable reason.
    /// </summary>
    public const string Reason = "reason";

    /// <summary>
    /// Extension name carrying a stable machine-readable authorization reason code.
    /// </summary>
    public const string ReasonCode = "reasonCode";

    /// <summary>
    /// Extension name carrying retry guidance when the caller may try again later.
    /// </summary>
    public const string RetryAfter = "retryAfter";
}
