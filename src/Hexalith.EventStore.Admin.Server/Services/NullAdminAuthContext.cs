namespace Hexalith.EventStore.Admin.Server.Services;

/// <summary>
/// No-op implementation of <see cref="IAdminAuthContext"/>.
/// Used as the default when no ASP.NET Core HTTP context is available.
/// Story 14-3 replaces this with a real implementation.
/// </summary>
public sealed class NullAdminAuthContext : IAdminAuthContext
{
    /// <inheritdoc/>
    public string? GetToken() => null;

    /// <inheritdoc/>
    public string? GetUserId() => null;
}
