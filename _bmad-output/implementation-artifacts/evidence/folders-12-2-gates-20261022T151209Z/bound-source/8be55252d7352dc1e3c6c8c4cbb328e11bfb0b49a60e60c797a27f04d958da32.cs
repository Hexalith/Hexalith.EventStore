namespace Hexalith.EventStore.Contracts.Authorization;

/// <summary>
/// Converts authorization failure categories to and from stable public reason-code strings.
/// </summary>
public static class AuthorizationFailureReasonExtensions {
    /// <summary>Reason code for missing or invalid authentication.</summary>
    public const string AuthenticationRequired = "authentication_required";
    /// <summary>Reason code for a missing subject claim.</summary>
    public const string SubjectMissing = "subject_missing";
    /// <summary>Reason code for a missing tenant identifier.</summary>
    public const string TenantMissing = "tenant_missing";
    /// <summary>Reason code for conflicting tenant sources.</summary>
    public const string TenantMismatch = "tenant_mismatch";
    /// <summary>Reason code for an unknown tenant.</summary>
    public const string TenantNotFound = "tenant_not_found";
    /// <summary>Reason code for a disabled tenant.</summary>
    public const string TenantDisabled = "tenant_disabled";
    /// <summary>Reason code for a suspended tenant.</summary>
    public const string TenantSuspended = "tenant_suspended";
    /// <summary>Reason code for stale tenant authorization data.</summary>
    public const string TenantStale = "tenant_stale";
    /// <summary>Reason code for unavailable tenant authorization data.</summary>
    public const string TenantUnavailable = "tenant_unavailable";
    /// <summary>Reason code for ambiguous tenant authorization data.</summary>
    public const string TenantAmbiguous = "tenant_ambiguous";
    /// <summary>Reason code for a principal that is not a tenant member.</summary>
    public const string PrincipalNotMember = "principal_not_member";
    /// <summary>Reason code for an insufficient tenant role.</summary>
    public const string InsufficientRole = "insufficient_role";
    /// <summary>Reason code for insufficient command or query permission.</summary>
    public const string InsufficientPermission = "insufficient_permission";
    /// <summary>Reason code for unavailable authorization validation infrastructure.</summary>
    public const string AuthorizationServiceUnavailable = "authorization_service_unavailable";

    /// <summary>
    /// Converts a typed reason to the stable public reason-code string.
    /// </summary>
    public static string? ToReasonCode(this AuthorizationFailureReason reason) => reason switch {
        AuthorizationFailureReason.None => null,
        AuthorizationFailureReason.AuthenticationRequired => AuthenticationRequired,
        AuthorizationFailureReason.SubjectMissing => SubjectMissing,
        AuthorizationFailureReason.TenantMissing => TenantMissing,
        AuthorizationFailureReason.TenantMismatch => TenantMismatch,
        AuthorizationFailureReason.TenantNotFound => TenantNotFound,
        AuthorizationFailureReason.TenantDisabled => TenantDisabled,
        AuthorizationFailureReason.TenantSuspended => TenantSuspended,
        AuthorizationFailureReason.TenantStale => TenantStale,
        AuthorizationFailureReason.TenantUnavailable => TenantUnavailable,
        AuthorizationFailureReason.TenantAmbiguous => TenantAmbiguous,
        AuthorizationFailureReason.PrincipalNotMember => PrincipalNotMember,
        AuthorizationFailureReason.InsufficientRole => InsufficientRole,
        AuthorizationFailureReason.InsufficientPermission => InsufficientPermission,
        AuthorizationFailureReason.AuthorizationServiceUnavailable => AuthorizationServiceUnavailable,
        _ => AuthorizationServiceUnavailable,
    };

    /// <summary>
    /// Converts a public reason-code string to a typed reason.
    /// </summary>
    public static AuthorizationFailureReason FromReasonCode(
        string? reasonCode,
        AuthorizationFailureReason unknownFallback = AuthorizationFailureReason.AuthorizationServiceUnavailable) {
        if (string.IsNullOrWhiteSpace(reasonCode)) {
            return unknownFallback;
        }

        return reasonCode.Trim() switch {
            AuthenticationRequired => AuthorizationFailureReason.AuthenticationRequired,
            SubjectMissing => AuthorizationFailureReason.SubjectMissing,
            TenantMissing => AuthorizationFailureReason.TenantMissing,
            TenantMismatch => AuthorizationFailureReason.TenantMismatch,
            TenantNotFound => AuthorizationFailureReason.TenantNotFound,
            TenantDisabled => AuthorizationFailureReason.TenantDisabled,
            TenantSuspended => AuthorizationFailureReason.TenantSuspended,
            TenantStale => AuthorizationFailureReason.TenantStale,
            TenantUnavailable => AuthorizationFailureReason.TenantUnavailable,
            TenantAmbiguous => AuthorizationFailureReason.TenantAmbiguous,
            PrincipalNotMember => AuthorizationFailureReason.PrincipalNotMember,
            InsufficientRole => AuthorizationFailureReason.InsufficientRole,
            InsufficientPermission => AuthorizationFailureReason.InsufficientPermission,
            AuthorizationServiceUnavailable => AuthorizationFailureReason.AuthorizationServiceUnavailable,
            _ => unknownFallback,
        };
    }
}
