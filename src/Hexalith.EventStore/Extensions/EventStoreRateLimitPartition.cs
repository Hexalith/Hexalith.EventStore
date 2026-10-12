using System.Security.Claims;

using Hexalith.EventStore.Authentication;
using Hexalith.EventStore.ServiceDefaults.Authentication;

namespace Hexalith.EventStore.Extensions;

/// <summary>Separates verified internal traffic from unauthenticated traffic without relaxing either quota.</summary>
internal static class EventStoreRateLimitPartition {
    public static string? TrustedInternalKey(ClaimsPrincipal? principal) {
        if (principal is null) {
            return null;
        }

        foreach (ClaimsIdentity identity in principal.Identities) {
            if (!identity.IsAuthenticated) {
                continue;
            }

            if (string.Equals(identity.AuthenticationType, DaprInternalAuthenticationOptions.SchemeName, StringComparison.Ordinal)) {
                string? caller = identity.FindFirst(EventStoreWorkloadAuthenticationDefaults.WorkloadClaimType)?.Value;
                string? subject = identity.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!string.IsNullOrWhiteSpace(caller) && (subject is null || string.Equals(subject, "workload:" + caller, StringComparison.Ordinal))) {
                    return "workload:" + caller;
                }
            }

            if (string.Equals(identity.AuthenticationType, EventStoreWorkloadAuthenticationDefaults.SidecarChannelScheme, StringComparison.Ordinal)
                && string.Equals(identity.FindFirst(EventStoreWorkloadAuthenticationDefaults.ChannelClaimType)?.Value,
                    DaprSidecarChannelAuthenticationHandler.VerifiedChannel, StringComparison.Ordinal)) {
                return "sidecar:verified";
            }
        }

        return null;
    }

    public static string? TrustedTenantKey(ClaimsPrincipal? principal) {
        if (principal is null) {
            return null;
        }

        foreach (ClaimsIdentity identity in principal.Identities) {
            if (!identity.IsAuthenticated) {
                continue;
            }

            if (string.Equals(identity.AuthenticationType, DaprInternalAuthenticationOptions.SchemeName, StringComparison.Ordinal)) {
                string? boundTenant = identity.FindFirst(EventStoreWorkloadAuthenticationDefaults.TenantBindingClaimType)?.Value;
                if (!string.IsNullOrWhiteSpace(boundTenant)) {
                    return boundTenant;
                }
            }
        }

        return null;
    }
}
