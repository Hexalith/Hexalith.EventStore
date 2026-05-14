using Hexalith.EventStore.Contracts.Authorization;

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
    public const string TenantNotFound = "https://hexalith.io/problems/tenant-not-found";
    public const string TenantDisabled = "https://hexalith.io/problems/tenant-disabled";
    public const string TenantSuspended = "https://hexalith.io/problems/tenant-suspended";
    public const string TenantStale = "https://hexalith.io/problems/tenant-stale";
    public const string TenantAmbiguous = "https://hexalith.io/problems/tenant-ambiguous";
    public const string PrincipalNotMember = "https://hexalith.io/problems/principal-not-member";
    public const string InsufficientRole = "https://hexalith.io/problems/insufficient-role";
    public const string InsufficientPermission = "https://hexalith.io/problems/insufficient-permission";
    public const string NotFound = "https://hexalith.io/problems/not-found";
    public const string NotImplemented = "https://hexalith.io/problems/not-implemented";
    public const string ConcurrencyConflict = "https://hexalith.io/problems/concurrency-conflict";
    public const string RateLimitExceeded = "https://hexalith.io/problems/rate-limit-exceeded";
    public const string BackpressureExceeded = "https://hexalith.io/problems/backpressure-exceeded";
    public const string ServiceUnavailable = "https://hexalith.io/problems/service-unavailable";
    public const string CommandStatusNotFound = "https://hexalith.io/problems/command-status-not-found";
    public const string InternalServerError = "https://hexalith.io/problems/internal-server-error";

    public static string FromAuthorizationReasonCode(string? reasonCode) => reasonCode switch {
        AuthorizationReasonCodes.AuthenticationRequired => AuthenticationRequired,
        AuthorizationReasonCodes.TenantNotFound => TenantNotFound,
        AuthorizationReasonCodes.TenantDisabled => TenantDisabled,
        AuthorizationReasonCodes.TenantSuspended => TenantSuspended,
        AuthorizationReasonCodes.TenantStale => TenantStale,
        AuthorizationReasonCodes.TenantAmbiguous => TenantAmbiguous,
        AuthorizationReasonCodes.PrincipalNotMember => PrincipalNotMember,
        AuthorizationReasonCodes.InsufficientRole => InsufficientRole,
        AuthorizationReasonCodes.InsufficientPermission => InsufficientPermission,
        AuthorizationReasonCodes.AuthorizationServiceUnavailable => ServiceUnavailable,
        AuthorizationReasonCodes.TenantUnavailable => ServiceUnavailable,
        _ => Forbidden,
    };
}
