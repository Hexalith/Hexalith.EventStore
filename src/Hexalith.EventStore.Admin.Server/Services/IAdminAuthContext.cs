namespace Hexalith.EventStore.Admin.Server.Services;

/// <summary>
/// Provides the caller's authorization context for service-to-service calls.
/// All InvokeMethodAsync calls to CommandApi/Tenants must forward the JWT token,
/// or the target service will reject with 401/403.
/// Story 14-3 registers the ASP.NET Core implementation that extracts the token
/// from IHttpContextAccessor. For Tier 1 tests, mock this interface.
/// </summary>
public interface IAdminAuthContext
{
    /// <summary>
    /// Gets the caller's JWT bearer token, or null if not available.
    /// </summary>
    /// <returns>The JWT token string, or null.</returns>
    string? GetToken();

    /// <summary>
    /// Gets the caller's user identifier, or null if not available.
    /// </summary>
    /// <returns>The user identifier, or null.</returns>
    string? GetUserId();
}
