using System.Security.Claims;
using System.Text.Json;

using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;

namespace Hexalith.EventStore.Admin.Server.Authorization;

/// <summary>
/// Maps existing JWT claims to <c>eventstore:admin-role</c> for admin authorization policies.
/// Works with any OIDC provider — no IdP configuration changes needed.
/// </summary>
public class AdminClaimsTransformation(ILogger<AdminClaimsTransformation> logger) : IClaimsTransformation
{
    /// <inheritdoc/>
    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        try
        {
            if (principal.Identity is not ClaimsIdentity identity || !identity.IsAuthenticated)
            {
                return Task.FromResult(principal);
            }

            // Idempotency: do not add duplicate admin role claims
            if (identity.HasClaim(c => c.Type == AdminClaimTypes.AdminRole))
            {
                return Task.FromResult(principal);
            }

            if (IsGlobalAdministrator(principal))
            {
                identity.AddClaim(new Claim(AdminClaimTypes.AdminRole, nameof(Abstractions.Models.Common.AdminRole.Admin)));
                return Task.FromResult(principal);
            }

            if (HasOperatorPermission(principal))
            {
                identity.AddClaim(new Claim(AdminClaimTypes.AdminRole, nameof(Abstractions.Models.Common.AdminRole.Operator)));
                return Task.FromResult(principal);
            }

            if (HasTenantClaim(principal))
            {
                identity.AddClaim(new Claim(AdminClaimTypes.AdminRole, nameof(Abstractions.Models.Common.AdminRole.ReadOnly)));
                return Task.FromResult(principal);
            }

            // No admin-relevant claims — no claim added; all admin endpoints return 403
            return Task.FromResult(principal);
        }
        catch (Exception ex)
        {
            // Exception-safe: return identity unchanged, log warning
            logger.LogWarning(ex, "Admin claims transformation failed. Returning identity unchanged.");
            return Task.FromResult(principal);
        }
    }

    private static bool HasTenantClaim(ClaimsPrincipal principal)
    {
        return principal.HasClaim(c =>
            c.Type == AdminClaimTypes.Tenant && !string.IsNullOrWhiteSpace(c.Value));
    }

    private static bool HasOperatorPermission(ClaimsPrincipal principal)
    {
        // Only command:replay specifically — NOT any eventstore:permission
        return principal.HasClaim("eventstore:permission", "command:replay");
    }

    private static bool IsGlobalAdministrator(ClaimsPrincipal principal)
    {
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
