
using System.Security.Claims;

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

        var tenantClaims = user.FindAll("eventstore:tenant")
            .Select(c => c.Value)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .ToList();

        if (tenantClaims.Count == 0) {
            return Task.FromResult(TenantValidationResult.Denied("No tenant authorization claims found. Access denied."));
        }

        // Case-SENSITIVE comparison (StringComparison.Ordinal) — tenant IDs are system-assigned
        if (!tenantClaims.Any(t => string.Equals(t, tenantId, StringComparison.Ordinal))) {
            return Task.FromResult(TenantValidationResult.Denied($"Not authorized for tenant '{tenantId}'."));
        }

        return Task.FromResult(TenantValidationResult.Allowed);
    }
}
