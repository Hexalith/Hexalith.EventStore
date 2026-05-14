using Hexalith.EventStore.Contracts.Authorization;

namespace Hexalith.EventStore.Authorization;

internal static class AuthorizationReasonCodeMapper {
    public static string TenantFromText(string? reason) {
        if (string.IsNullOrWhiteSpace(reason)) {
            return AuthorizationReasonCodes.TenantMissing;
        }

        string normalized = reason.ToUpperInvariant();
        if (normalized.Contains("NOT FOUND", StringComparison.Ordinal)) {
            return AuthorizationReasonCodes.TenantNotFound;
        }

        if (normalized.Contains("DISABLED", StringComparison.Ordinal)) {
            return AuthorizationReasonCodes.TenantDisabled;
        }

        if (normalized.Contains("SUSPENDED", StringComparison.Ordinal) || normalized.Contains("INACTIVE", StringComparison.Ordinal)) {
            return AuthorizationReasonCodes.TenantSuspended;
        }

        if (normalized.Contains("STALE", StringComparison.Ordinal)) {
            return AuthorizationReasonCodes.TenantStale;
        }

        if (normalized.Contains("AMBIGUOUS", StringComparison.Ordinal)) {
            return AuthorizationReasonCodes.TenantAmbiguous;
        }

        if (normalized.Contains("UNAVAILABLE", StringComparison.Ordinal) || normalized.Contains("UNREACHABLE", StringComparison.Ordinal)) {
            return AuthorizationReasonCodes.TenantUnavailable;
        }

        return normalized.Contains("NO TENANT", StringComparison.Ordinal)
            ? AuthorizationReasonCodes.TenantMissing
            : AuthorizationReasonCodes.PrincipalNotMember;
    }

    public static string RbacFromText(string? reason) {
        if (string.IsNullOrWhiteSpace(reason)) {
            return AuthorizationReasonCodes.InsufficientPermission;
        }

        string normalized = reason.ToUpperInvariant();
        if (normalized.Contains("ROLE", StringComparison.Ordinal)) {
            return AuthorizationReasonCodes.InsufficientRole;
        }

        return AuthorizationReasonCodes.InsufficientPermission;
    }
}
