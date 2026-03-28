namespace Hexalith.EventStore.ErrorHandling;

/// <summary>
/// Centralized constants for ProblemDetails type URIs (UX-DR7).
/// Each URI identifies a unique error category and resolves to documentation.
/// </summary>
public static class ProblemTypeUris {
    public const string ValidationError = "https://hexalith.io/problems/validation-error";
    public const string AuthenticationRequired = "https://hexalith.io/problems/authentication-required";
    public const string TokenExpired = "https://hexalith.io/problems/token-expired";
    public const string BadRequest = "https://hexalith.io/problems/bad-request";
    public const string Forbidden = "https://hexalith.io/problems/forbidden";
    public const string NotFound = "https://hexalith.io/problems/not-found";
    public const string NotImplemented = "https://hexalith.io/problems/not-implemented";
    public const string ConcurrencyConflict = "https://hexalith.io/problems/concurrency-conflict";
    public const string RateLimitExceeded = "https://hexalith.io/problems/rate-limit-exceeded";
    public const string BackpressureExceeded = "https://hexalith.io/problems/backpressure-exceeded";
    public const string ServiceUnavailable = "https://hexalith.io/problems/service-unavailable";
    public const string CommandStatusNotFound = "https://hexalith.io/problems/command-status-not-found";
    public const string InternalServerError = "https://hexalith.io/problems/internal-server-error";
}
