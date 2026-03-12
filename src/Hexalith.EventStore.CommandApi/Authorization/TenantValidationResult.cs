
namespace Hexalith.EventStore.CommandApi.Authorization;

/// <summary>
/// Result of tenant authorization validation.
/// </summary>
/// <param name="IsAuthorized">Whether the tenant access is authorized.</param>
/// <param name="Reason">The reason for denial, or null if authorized.</param>
public record TenantValidationResult(bool IsAuthorized, string? Reason = null) {
    /// <summary>
    /// Gets an authorized result.
    /// </summary>
    public static TenantValidationResult Allowed => new(true);

    /// <summary>
    /// Creates a denied result with a reason.
    /// </summary>
    /// <param name="reason">The reason for denial.</param>
    /// <returns>A denied <see cref="TenantValidationResult"/>.</returns>
    public static TenantValidationResult Denied(string reason) => new(false, reason);
}
