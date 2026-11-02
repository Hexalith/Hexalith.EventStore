using Hexalith.EventStore.Contracts.Authorization;

namespace Hexalith.EventStore.Authorization;

/// <summary>
/// Result of tenant authorization validation.
/// </summary>
/// <param name="IsAuthorized">Whether the tenant access is authorized.</param>
/// <param name="Reason">The reason for denial, or null if authorized.</param>
/// <param name="ReasonCode">The stable failure reason code, or <see cref="AuthorizationFailureReason.None"/> if authorized.</param>
/// <param name="CanonicalTenantId">The validated lowercase tenant to forward after authorization.</param>
public record TenantValidationResult(
    bool IsAuthorized,
    string? Reason = null,
    AuthorizationFailureReason ReasonCode = AuthorizationFailureReason.None,
    string? CanonicalTenantId = null) {
    /// <summary>
    /// Gets an authorized result.
    /// </summary>
    public static TenantValidationResult Allowed => new(true);

    /// <summary>Creates an authorized result carrying the exact tenant to forward.</summary>
    /// <param name="canonicalTenantId">A validated lowercase public tenant.</param>
    /// <returns>The authorized result.</returns>
    public static TenantValidationResult AllowedFor(string canonicalTenantId) => new(true, CanonicalTenantId: canonicalTenantId);

    /// <summary>
    /// Creates a denied result with a reason.
    /// </summary>
    /// <param name="reason">The reason for denial.</param>
    /// <param name="reasonCode">The stable machine-readable denial reason.</param>
    /// <returns>A denied <see cref="TenantValidationResult"/>.</returns>
    public static TenantValidationResult Denied(
        string reason,
        AuthorizationFailureReason reasonCode = AuthorizationFailureReason.PrincipalNotMember)
        => new(false, reason, reasonCode);
}
