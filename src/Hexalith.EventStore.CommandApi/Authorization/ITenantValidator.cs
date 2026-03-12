
using System.Security.Claims;

namespace Hexalith.EventStore.CommandApi.Authorization;

/// <summary>
/// Validates whether a user is authorized to access a specific tenant.
/// API-level authorization concern (Layer 4 in the six-layer auth model).
/// </summary>
/// <remarks>
/// This is distinct from <see cref="Server.Actors.ITenantValidator"/> which provides
/// defense-in-depth tenant isolation at the DAPR actor level (Layer 5).
/// </remarks>
public interface ITenantValidator {
    /// <summary>
    /// Validates whether the user is authorized to access the specified tenant.
    /// </summary>
    /// <param name="user">The authenticated user's claims principal.</param>
    /// <param name="tenantId">The tenant ID to validate access for.</param>
    /// <param name="aggregateId">Optional aggregate identifier for fine-grained authorization checks.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="TenantValidationResult"/> indicating whether access is authorized.</returns>
    Task<TenantValidationResult> ValidateAsync(ClaimsPrincipal user, string tenantId, CancellationToken cancellationToken, string? aggregateId = null);
}
