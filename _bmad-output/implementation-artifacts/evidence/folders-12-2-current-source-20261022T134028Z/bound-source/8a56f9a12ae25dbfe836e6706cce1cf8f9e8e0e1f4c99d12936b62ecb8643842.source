using System.Security.Claims;
using Hexalith.EventStore.Contracts.Queries;

namespace Hexalith.EventStore.Queries;

/// <summary>Captures authority only from the currently authenticated gateway caller after gateway authorization.</summary>
internal static class GatewayQueryCallerAuthority
{
    internal static QueryCallerAuthority? Capture(ClaimsPrincipal principal, IConfiguration? configuration, string domain, string queryType)
    {
        ArgumentNullException.ThrowIfNull(principal);
        // Existing domain queries keep their original transport contract. Only explicitly configured
        // authority-dependent queries require bound signing; unsupported issuer profiles fail closed.
        if (configuration?.GetSection($"Authentication:QueryAuthority:RequiredQueries:{domain}").GetChildren()
            .Any(item => string.Equals(item.Value, queryType, StringComparison.Ordinal)) != true) return null;
        if (principal.Identity?.IsAuthenticated != true) throw new InvalidOperationException("Authenticated query caller required.");
        string[] roleTypes = principal.Identities.Select(static identity => identity.RoleClaimType).Concat([ClaimTypes.Role, "role", domain + ":role"])
            .Where(type => !type.EndsWith(":role", StringComparison.Ordinal) || type == domain + ":role").Distinct(StringComparer.Ordinal).ToArray();
        string[] roles = principal.Claims.Where(claim => roleTypes.Contains(claim.Type, StringComparer.Ordinal)).Select(static claim => claim.Value).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        string[] permissions = principal.FindAll("eventstore:permission").Select(static claim => claim.Value).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        if (roles.Length > 64 || permissions.Length > 64 || roles.Concat(permissions).Any(static value => string.IsNullOrWhiteSpace(value) || value.Length > 512))
            throw new InvalidOperationException("Query authority exceeds its bounded contract.");
        return new("authenticated-caller", roles, permissions);
    }
}
