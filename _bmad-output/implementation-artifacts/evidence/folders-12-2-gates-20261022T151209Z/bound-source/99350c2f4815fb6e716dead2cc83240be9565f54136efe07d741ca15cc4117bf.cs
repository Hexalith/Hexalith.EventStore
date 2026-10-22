namespace Hexalith.EventStore.Contracts.Authorization;

/// <summary>
/// Stable authorization failure categories exposed by EventStore gateway contracts.
/// </summary>
public enum AuthorizationFailureReason {
    /// <summary>No authorization failure occurred.</summary>
    None = 0,

    /// <summary>The request did not provide valid authentication.</summary>
    AuthenticationRequired,

    /// <summary>The authenticated principal does not contain a required subject identifier.</summary>
    SubjectMissing,

    /// <summary>The request did not provide a tenant identifier.</summary>
    TenantMissing,

    /// <summary>The request tenant conflicts with another tenant source.</summary>
    TenantMismatch,

    /// <summary>The tenant authority could not find the tenant.</summary>
    TenantNotFound,

    /// <summary>The tenant exists but is disabled.</summary>
    TenantDisabled,

    /// <summary>The tenant exists but is suspended or equivalent inactive state.</summary>
    TenantSuspended,

    /// <summary>The tenant authorization data is stale.</summary>
    TenantStale,

    /// <summary>The tenant authorization authority is unavailable.</summary>
    TenantUnavailable,

    /// <summary>The tenant authorization state is ambiguous.</summary>
    TenantAmbiguous,

    /// <summary>The authenticated principal is not a member of the tenant.</summary>
    PrincipalNotMember,

    /// <summary>The authenticated principal lacks the required role.</summary>
    InsufficientRole,

    /// <summary>The authenticated principal lacks the required permission.</summary>
    InsufficientPermission,

    /// <summary>The authorization validation service is unavailable.</summary>
    AuthorizationServiceUnavailable,
}
