using System.Security.Claims;

using Microsoft.AspNetCore.Http;

namespace Hexalith.EventStore.Admin.Server.Services;

/// <summary>
/// Reads the caller's auth context from the current ASP.NET Core HTTP request.
/// </summary>
public sealed class HttpContextAdminAuthContext(IHttpContextAccessor httpContextAccessor) : IAdminAuthContext {
    /// <inheritdoc/>
    public string? GetToken() {
        string? headerValue = httpContextAccessor.HttpContext?.Request.Headers.Authorization;
        if (string.IsNullOrWhiteSpace(headerValue)) {
            return null;
        }

        const string BearerPrefix = "Bearer ";
        if (!headerValue.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase)) {
            return null;
        }

        string token = headerValue[BearerPrefix.Length..].Trim();
        return string.IsNullOrWhiteSpace(token) ? null : token;
    }

    /// <inheritdoc/>
    public string? GetUserId() {
        ClaimsPrincipal? user = httpContextAccessor.HttpContext?.User;
        return user?.FindFirst("sub")?.Value
            ?? user?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }
}