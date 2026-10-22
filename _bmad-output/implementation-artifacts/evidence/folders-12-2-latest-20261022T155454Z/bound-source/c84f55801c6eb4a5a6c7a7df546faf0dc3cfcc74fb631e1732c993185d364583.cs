namespace Hexalith.EventStore.Contracts.Problems;

/// <summary>
/// Stable RFC 7807 ProblemDetails extension names used by EventStore gateway clients.
/// </summary>
public static class GatewayProblemDetailsExtensions {
    /// <summary>Extension name carrying a stable canonical error code.</summary>
    public const string Code = "code";

    /// <summary>Extension name carrying a stable canonical error category.</summary>
    public const string Category = "category";

    /// <summary>Extension name indicating whether repeating the same request may succeed.</summary>
    public const string Retryable = "retryable";

    /// <summary>Extension name carrying stable client remediation guidance.</summary>
    public const string ClientAction = "clientAction";

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

    /// <summary>
    /// Extension name carrying the originating domain rejection event type for typed
    /// domain rejection ProblemDetails responses.
    /// </summary>
    public const string RejectionType = "rejectionType";

    /// <summary>
    /// Extension name carrying bounded, human-readable corrective action guidance for
    /// the caller in response to a domain rejection or recoverable failure.
    /// </summary>
    public const string CorrectiveAction = "correctiveAction";

    /// <summary>
    /// Extension name carrying a stable degradation classification when an internal
    /// component returns a bounded failure ProblemDetails to the caller.
    /// </summary>
    public const string Degradation = "degradation";
}
