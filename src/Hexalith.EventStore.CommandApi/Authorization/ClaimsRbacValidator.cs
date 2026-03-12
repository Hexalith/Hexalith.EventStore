
using System.Security.Claims;

using Hexalith.EventStore.CommandApi.Pipeline;

namespace Hexalith.EventStore.CommandApi.Authorization;

/// <summary>
/// Claims-based RBAC validator. Extracts domain and permission authorization logic from
/// <c>AuthorizationBehavior.cs</c> (lines 46-84).
/// Used as the default when <c>EventStoreAuthorizationOptions.RbacValidatorActorName</c> is null.
/// </summary>
/// <remarks>
/// The <paramref name="messageCategory"/> parameter is accepted but does NOT influence
/// claims-based validation results. Claims-based authorization does not distinguish
/// read vs write operations. This parameter exists for Story 17-2's actor-based
/// implementation which CAN discriminate by category.
/// </remarks>
public class ClaimsRbacValidator : IRbacValidator {
    /// <inheritdoc/>
    public Task<RbacValidationResult> ValidateAsync(
        ClaimsPrincipal user,
        string tenantId,
        string domain,
        string messageType,
        string messageCategory,
        CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(user);

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
            bool hasWildcard = permissionClaims.Any(p => string.Equals(p, AuthorizationConstants.WildcardPermission, StringComparison.OrdinalIgnoreCase));
            bool hasSubmit = permissionClaims.Any(p => string.Equals(p, AuthorizationConstants.SubmitPermission, StringComparison.OrdinalIgnoreCase));
            bool hasSpecific = permissionClaims.Any(p => string.Equals(p, messageType, StringComparison.OrdinalIgnoreCase));
            string typeLabel = string.Equals(messageCategory, "query", StringComparison.OrdinalIgnoreCase)
                ? "query type"
                : "command type";

            if (!hasWildcard && !hasSubmit && !hasSpecific) {
                return Task.FromResult(RbacValidationResult.Denied($"Not authorized for {typeLabel} '{messageType}'."));
            }
        }

        return Task.FromResult(RbacValidationResult.Allowed);
    }
}
