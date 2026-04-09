using System.Security.Claims;
using System.Text.Json;

namespace Hexalith.EventStore.Authorization;

/// <summary>
/// Shared helper for determining whether a <see cref="ClaimsPrincipal"/> represents
/// a global administrator. Checks boolean claims (<c>global_admin</c>, <c>is_global_admin</c>),
/// single-value role claims (<c>role</c>, <c>ClaimTypes.Role</c>), and multi-value
/// role claims (<c>roles</c> as JSON array or delimited string).
/// </summary>
public static class GlobalAdministratorHelper
{
    /// <summary>
    /// Determines whether the specified principal is a global administrator.
    /// </summary>
    /// <param name="principal">The claims principal to evaluate.</param>
    /// <returns><c>true</c> if the principal has a global administrator claim; otherwise, <c>false</c>.</returns>
    public static bool IsGlobalAdministrator(ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        foreach (Claim claim in principal.Claims)
        {
            if (IsGlobalAdministratorClaim(claim))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsGlobalAdministratorClaim(Claim claim)
    {
        if (claim.Type is "global_admin" or "is_global_admin")
        {
            return bool.TryParse(claim.Value, out bool isGlobalAdmin) && isGlobalAdmin;
        }

        if (claim.Type is ClaimTypes.Role or "role")
        {
            return IsGlobalAdministratorValue(claim.Value);
        }

        if (claim.Type == "roles")
        {
            return ClaimValueContainsGlobalAdministrator(claim.Value);
        }

        return false;
    }

    private static bool ClaimValueContainsGlobalAdministrator(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (value.StartsWith('['))
        {
            try
            {
                string[]? roles = JsonSerializer.Deserialize<string[]>(value);
                if (roles is not null)
                {
                    return roles.Any(IsGlobalAdministratorValue);
                }
            }
            catch (JsonException)
            {
                // Fall through to delimiter-based parsing below.
            }
        }

        return value.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries)
            .Any(IsGlobalAdministratorValue);
    }

    private static bool IsGlobalAdministratorValue(string value)
        => value is not null
            && (string.Equals(value, "GlobalAdministrator", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "global-administrator", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "global-admin", StringComparison.OrdinalIgnoreCase));
}
