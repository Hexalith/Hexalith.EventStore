using Hexalith.EventStore.Contracts.Authorization;

namespace Hexalith.EventStore.Authorization;

/// <summary>
/// Result of RBAC (role-based access control) authorization validation.
/// </summary>
/// <param name="IsAuthorized">Whether the RBAC check passed.</param>
/// <param name="Reason">The reason for denial, or null if authorized.</param>
/// <param name="ReasonCode">The stable failure reason code, or <see cref="AuthorizationFailureReason.None"/> if authorized.</param>
public record RbacValidationResult(
    bool IsAuthorized,
    string? Reason = null,
    AuthorizationFailureReason ReasonCode = AuthorizationFailureReason.None) {
    /// <summary>
    /// Gets an authorized result.
    /// </summary>
    public static RbacValidationResult Allowed => new(true);

    /// <summary>
    /// Creates a denied result with a reason.
    /// </summary>
    /// <param name="reason">The reason for denial.</param>
    /// <param name="reasonCode">The stable machine-readable denial reason.</param>
    /// <returns>A denied <see cref="RbacValidationResult"/>.</returns>
    public static RbacValidationResult Denied(
        string reason,
        AuthorizationFailureReason reasonCode = AuthorizationFailureReason.InsufficientPermission)
        => new(false, reason, reasonCode);
}
