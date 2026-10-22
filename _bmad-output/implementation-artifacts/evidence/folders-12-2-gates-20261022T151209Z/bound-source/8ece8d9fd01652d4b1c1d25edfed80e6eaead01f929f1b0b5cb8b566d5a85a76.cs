using Hexalith.EventStore.Admin.Abstractions.Models.Common;

using Microsoft.AspNetCore.Components.Authorization;

namespace Hexalith.EventStore.Admin.UI.Services;

/// <summary>
/// Extracts the AdminRole from the current user's JWT claims
/// for role-based UI rendering decisions.
/// </summary>
public sealed class AdminUserContext(AuthenticationStateProvider authStateProvider) {
    /// <summary>
    /// Gets the current user's admin role from JWT claims.
    /// Returns <see cref="AdminRole.ReadOnly"/> if no role claim is found.
    /// </summary>
    public async Task<AdminRole> GetRoleAsync(CancellationToken cancellationToken = default) {
        AuthenticationState authState = await authStateProvider.GetAuthenticationStateAsync().ConfigureAwait(false);
        if (TryResolveRole(authState, out AdminRole role)) {
            return role;
        }

        return AdminRole.ReadOnly;
    }

    /// <summary>
    /// Checks whether the principal is a global administrator by examining boolean claims,
    /// single-value role claims, and multi-value role claims. Mirrors the broad check in
    /// <c>Hexalith.EventStore.Authorization.GlobalAdministratorHelper</c>.
    /// </summary>
    private static bool IsGlobalAdministrator(System.Security.Claims.ClaimsPrincipal principal) {
        foreach (System.Security.Claims.Claim claim in principal.Claims) {
            if (claim.Type is "global_admin" or "is_global_admin") {
                if (bool.TryParse(claim.Value, out bool isGlobalAdmin) && isGlobalAdmin) {
                    return true;
                }
            }
            else if (claim.Type is System.Security.Claims.ClaimTypes.Role or "role") {
                if (IsGlobalAdministratorValue(claim.Value)) {
                    return true;
                }
            }
            else if (claim.Type == "roles" && ContainsGlobalAdministratorValue(claim.Value)) {
                return true;
            }
        }

        return false;
    }

    private static bool IsGlobalAdministratorValue(string value)
        => string.Equals(value, "GlobalAdministrator", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "global-administrator", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "global-admin", StringComparison.OrdinalIgnoreCase);

    private static bool ContainsGlobalAdministratorValue(string value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return false;
        }

        if (value.StartsWith('[')) {
            try {
                string[]? roles = System.Text.Json.JsonSerializer.Deserialize<string[]>(value);
                if (roles is not null) {
                    return roles.Any(IsGlobalAdministratorValue);
                }
            }
            catch (System.Text.Json.JsonException) {
                // Fall through to delimiter-based parsing below.
            }
        }

        return value.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries)
            .Any(IsGlobalAdministratorValue);
    }

    /// <summary>
    /// Checks if the current user has at least the specified role level.
    /// Admin > Operator > ReadOnly.
    /// </summary>
    public async Task<bool> HasMinimumRoleAsync(AdminRole minimumRole, CancellationToken cancellationToken = default) {
        AuthenticationState authState = await authStateProvider.GetAuthenticationStateAsync().ConfigureAwait(false);
        return TryResolveRole(authState, out AdminRole currentRole) && currentRole >= minimumRole;
    }

    private static bool TryResolveRole(AuthenticationState authState, out AdminRole role) {
        if (authState.User.Identity?.IsAuthenticated != true) {
            role = default;
            return false;
        }

        System.Security.Claims.Claim[] explicitRoles = authState.User.FindAll(AdminClaimTypes.Role).ToArray();
        if (explicitRoles.Length > 0) {
            AdminRole? highestRole = null;
            foreach (System.Security.Claims.Claim claim in explicitRoles) {
                AdminRole? recognizedRole = claim.Value switch {
                    nameof(AdminRole.ReadOnly) => AdminRole.ReadOnly,
                    nameof(AdminRole.Operator) => AdminRole.Operator,
                    nameof(AdminRole.Admin) => AdminRole.Admin,
                    _ => null,
                };
                if (recognizedRole.HasValue && (!highestRole.HasValue || recognizedRole.Value > highestRole.Value)) {
                    highestRole = recognizedRole;
                }
            }

            role = highestRole.GetValueOrDefault();
            return highestRole.HasValue;
        }

        if (IsGlobalAdministrator(authState.User)) {
            role = AdminRole.Admin;
            return true;
        }

        if (authState.User.HasClaim("eventstore:permission", "command:replay")) {
            role = AdminRole.Operator;
            return true;
        }

        if (authState.User.HasClaim(claim => claim.Type == "eventstore:tenant" && !string.IsNullOrWhiteSpace(claim.Value))) {
            role = AdminRole.ReadOnly;
            return true;
        }

        role = default;
        return false;
    }
}
