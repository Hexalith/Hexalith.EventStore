using Hexalith.EventStore.Admin.Abstractions.Models.Common;

using Microsoft.AspNetCore.Components.Authorization;

namespace Hexalith.EventStore.Admin.UI.Services;

/// <summary>
/// Extracts the AdminRole from the current user's JWT claims
/// for role-based UI rendering decisions.
/// </summary>
public sealed class AdminUserContext(AuthenticationStateProvider authStateProvider)
{
    /// <summary>
    /// Gets the current user's admin role from JWT claims.
    /// Returns <see cref="AdminRole.ReadOnly"/> if no role claim is found.
    /// </summary>
    public async Task<AdminRole> GetRoleAsync(CancellationToken cancellationToken = default)
    {
        AuthenticationState authState = await authStateProvider.GetAuthenticationStateAsync().ConfigureAwait(false);
        string? roleClaim = authState.User.FindFirst(AdminClaimTypes.Role)?.Value;

        if (!string.IsNullOrEmpty(roleClaim)
            && Enum.TryParse<AdminRole>(roleClaim, ignoreCase: true, out AdminRole role))
        {
            return role;
        }

        // Fallback: Keycloak JWT has global_admin claim instead of
        // eventstore:admin-role (AdminClaimsTransformation runs on
        // Admin.Server only, not Admin.UI).
        if (IsGlobalAdministrator(authState.User))
        {
            return AdminRole.Admin;
        }

        return AdminRole.ReadOnly;
    }

    private static bool IsGlobalAdministrator(System.Security.Claims.ClaimsPrincipal principal)
    {
        string? value = principal.FindFirst("global_admin")?.Value
            ?? principal.FindFirst("is_global_admin")?.Value;
        return bool.TryParse(value, out bool isAdmin) && isAdmin;
    }

    /// <summary>
    /// Checks if the current user has at least the specified role level.
    /// Admin > Operator > ReadOnly.
    /// </summary>
    public async Task<bool> HasMinimumRoleAsync(AdminRole minimumRole, CancellationToken cancellationToken = default)
    {
        AdminRole currentRole = await GetRoleAsync(cancellationToken).ConfigureAwait(false);
        return currentRole >= minimumRole;
    }
}
