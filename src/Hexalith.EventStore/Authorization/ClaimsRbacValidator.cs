
using System.Security.Claims;

using Hexalith.EventStore.Pipeline;

namespace Hexalith.EventStore.Authorization;

/// <summary>
/// Claims-based RBAC validator. Extracts domain and permission authorization logic from
/// <c>AuthorizationBehavior.cs</c> (lines 46-84).
/// Used as the default when <c>EventStoreAuthorizationOptions.RbacValidatorActorName</c> is null.
/// </summary>
/// <remarks>
/// The <c>messageCategory</c> parameter determines which permission set is checked.
/// For "command" category: accepts <c>commands:*</c> (wildcard) or <c>command:submit</c>.
/// For "query" category: accepts <c>queries:*</c> (wildcard), <c>query:read</c>,
/// or the legacy <c>command:query</c> permission still used by the local Keycloak realm.
/// Both categories accept an exact <c>messageType</c> match.
/// </remarks>
public class ClaimsRbacValidator : IRbacValidator {
    private const string LegacyQueryPermission = "command:query";

    /// <inheritdoc/>
    public Task<RbacValidationResult> ValidateAsync(
        ClaimsPrincipal user,
        string tenantId,
        string domain,
        string messageType,
        string messageCategory,
        CancellationToken cancellationToken,
        string? aggregateId = null) {
        ArgumentNullException.ThrowIfNull(user);

        // Global administrators bypass domain and permission checks
        if (IsGlobalAdministrator(user)) {
            return Task.FromResult(RbacValidationResult.Allowed);
        }

        // Domain authorization: only enforce if user has domain claims
        var domainClaims = user.FindAll("eventstore:domain")
            .Select(c => c.Value)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .ToList();

        if (domainClaims.Count > 0 && !domainClaims.Any(d => string.Equals(d, domain, StringComparison.OrdinalIgnoreCase))) {
            return Task.FromResult(RbacValidationResult.Denied($"Not authorized for domain '{domain}'."));
        }

        // Permission authorization: only enforce if user has permission claims
        var permissionClaims = user.FindAll("eventstore:permission")
            .Select(c => c.Value)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .ToList();

        if (permissionClaims.Count > 0) {
            bool isQuery = string.Equals(messageCategory, "query", StringComparison.OrdinalIgnoreCase);
            bool hasWildcard;
            bool hasCategoryPermission;

            if (isQuery) {
                hasWildcard = permissionClaims.Any(p => string.Equals(p, AuthorizationConstants.QueryWildcardPermission, StringComparison.OrdinalIgnoreCase));
                hasCategoryPermission = permissionClaims.Any(p =>
                    string.Equals(p, AuthorizationConstants.ReadPermission, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(p, LegacyQueryPermission, StringComparison.OrdinalIgnoreCase));
            }
            else {
                hasWildcard = permissionClaims.Any(p => string.Equals(p, AuthorizationConstants.WildcardPermission, StringComparison.OrdinalIgnoreCase));
                hasCategoryPermission = permissionClaims.Any(p => string.Equals(p, AuthorizationConstants.SubmitPermission, StringComparison.OrdinalIgnoreCase));
            }

            bool hasSpecific = permissionClaims.Any(p => string.Equals(p, messageType, StringComparison.OrdinalIgnoreCase));
            string typeLabel = isQuery ? "query type" : "command type";

            if (!hasWildcard && !hasCategoryPermission && !hasSpecific) {
                return Task.FromResult(RbacValidationResult.Denied($"Not authorized for {typeLabel} '{messageType}'."));
            }
        }

        return Task.FromResult(RbacValidationResult.Allowed);
    }

    private static bool IsGlobalAdministrator(ClaimsPrincipal user) {
        Claim? claim = user.FindFirst("global_admin") ?? user.FindFirst("is_global_admin");
        return claim is not null && bool.TryParse(claim.Value, out bool isAdmin) && isAdmin;
    }
}
