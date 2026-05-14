namespace Hexalith.EventStore.Contracts.Authorization;

/// <summary>
/// Stable machine-readable reason codes for EventStore gateway authentication and authorization failures.
/// </summary>
public static class AuthorizationReasonCodes {
    /// <summary>
    /// The caller is not authenticated.
    /// </summary>
    public const string AuthenticationRequired = "authentication_required";

    /// <summary>
    /// The request did not contain a usable tenant identifier or tenant authorization claim.
    /// </summary>
    public const string TenantMissing = "tenant_missing";

    /// <summary>
    /// The tenant authority does not know the requested tenant.
    /// </summary>
    public const string TenantNotFound = "tenant_not_found";

    /// <summary>
    /// The tenant is disabled.
    /// </summary>
    public const string TenantDisabled = "tenant_disabled";

    /// <summary>
    /// The tenant is suspended or otherwise inactive.
    /// </summary>
    public const string TenantSuspended = "tenant_suspended";

    /// <summary>
    /// The tenant authority result is stale and cannot be trusted.
    /// </summary>
    public const string TenantStale = "tenant_stale";

    /// <summary>
    /// The tenant authority is unavailable.
    /// </summary>
    public const string TenantUnavailable = "tenant_unavailable";

    /// <summary>
    /// The tenant authority returned an ambiguous authorization state.
    /// </summary>
    public const string TenantAmbiguous = "tenant_ambiguous";

    /// <summary>
    /// The authenticated principal is not a member of the requested tenant.
    /// </summary>
    public const string PrincipalNotMember = "principal_not_member";

    /// <summary>
    /// The authenticated principal does not have the required tenant role.
    /// </summary>
    public const string InsufficientRole = "insufficient_role";

    /// <summary>
    /// The authenticated principal does not have the required permission.
    /// </summary>
    public const string InsufficientPermission = "insufficient_permission";

    /// <summary>
    /// The configured authorization service could not complete the validation.
    /// </summary>
    public const string AuthorizationServiceUnavailable = "authorization_service_unavailable";
}
