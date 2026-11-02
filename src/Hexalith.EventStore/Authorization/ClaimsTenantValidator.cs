
using System.Security.Claims;

using Hexalith.EventStore.Contracts.Authorization;
using Hexalith.EventStore.Contracts.Identity;

namespace Hexalith.EventStore.Authorization;

/// <summary>
/// Claims-based tenant validator. Extracts tenant authorization logic from
/// <c>CommandsController.cs</c> (lines 48-61).
/// Used as the default when <c>EventStoreAuthorizationOptions.TenantValidatorActorName</c> is null.
/// </summary>
public class ClaimsTenantValidator : ITenantValidator {
    /// <inheritdoc/>
    public Task<TenantValidationResult> ValidateAsync(
        ClaimsPrincipal user,
        string tenantId,
        CancellationToken cancellationToken,
        string? aggregateId = null) {
        ArgumentNullException.ThrowIfNull(user);

        if (string.IsNullOrEmpty(tenantId)) {
            return Task.FromResult(TenantValidationResult.Denied(
                "Tenant is required.",
                AuthorizationFailureReason.TenantMissing));
        }

        if (!TenantCanonicalizer.TryCanonicalize(tenantId, out string canonicalTenant)) {
            return Task.FromResult(TenantValidationResult.Denied(
                "Tenant is invalid or reserved.", AuthorizationFailureReason.TenantMismatch));
        }

        string[] tenantClaims = user.FindAll("eventstore:tenant").Select(c => c.Value).ToArray();

        if (tenantClaims.Length == 0) {
            return Task.FromResult(TenantValidationResult.Denied(
                "No tenant authorization claims found. Access denied.",
                AuthorizationFailureReason.PrincipalNotMember));
        }

        if (!TenantCanonicalizer.TryCanonicalizeGrants(tenantClaims, out IReadOnlySet<string> canonicalGrants)) {
            return Task.FromResult(TenantValidationResult.Denied(
                "Tenant authorization claims are invalid or ambiguous.", AuthorizationFailureReason.TenantAmbiguous));
        }

        if (!canonicalGrants.Contains(canonicalTenant)) {
            return Task.FromResult(TenantValidationResult.Denied(
                $"Not authorized for tenant '{canonicalTenant}'.",
                AuthorizationFailureReason.TenantMismatch));
        }

        return Task.FromResult(TenantValidationResult.AllowedFor(canonicalTenant));
    }

}
