
using System.Security.Claims;

namespace Hexalith.EventStore.CommandApi.Authorization;

/// <summary>
/// Validates role-based access control (RBAC) for domain and permission authorization.
/// API-level authorization concern (Layer 4 in the six-layer auth model).
/// </summary>
public interface IRbacValidator {
    /// <summary>
    /// Validates whether the user has RBAC authorization for the specified operation.
    /// </summary>
    /// <param name="user">The authenticated user's claims principal.</param>
    /// <param name="tenantId">The tenant ID for the operation.</param>
    /// <param name="domain">The domain name for domain claim checking.</param>
    /// <param name="messageType">The command type or query type name.</param>
    /// <param name="messageCategory">The message category: "command" or "query".</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="RbacValidationResult"/> indicating whether access is authorized.</returns>
    Task<RbacValidationResult> ValidateAsync(
        ClaimsPrincipal user,
        string tenantId,
        string domain,
        string messageType,
        string messageCategory,
        CancellationToken cancellationToken);
}
