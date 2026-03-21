using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;

namespace Hexalith.EventStore.Admin.Server.Authorization;

/// <summary>
/// Action filter that validates the caller's <c>eventstore:tenant</c> claims against
/// the requested tenant ID from route or query parameters (SEC-3 tenant isolation).
/// </summary>
public class AdminTenantAuthorizationFilter(ILogger<AdminTenantAuthorizationFilter> logger) : IAsyncActionFilter
{
    /// <inheritdoc/>
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        string? tenantId = context.RouteData.Values.TryGetValue("tenantId", out object? routeValue)
            ? routeValue?.ToString()
            : null;

        // Also check query string
        tenantId ??= context.HttpContext.Request.Query.TryGetValue("tenantId", out Microsoft.Extensions.Primitives.StringValues queryValue)
            ? queryValue.ToString()
            : null;

        if (string.IsNullOrWhiteSpace(tenantId))
        {
            // No tenantId in request — skip validation (tenant-agnostic endpoint)
            await next().ConfigureAwait(false);
            return;
        }

        // Admin-role users can access any tenant
        if (context.HttpContext.User.HasClaim(AdminClaimTypes.AdminRole, "Admin"))
        {
            await next().ConfigureAwait(false);
            return;
        }

        System.Collections.Generic.List<string> tenantClaims = context.HttpContext.User
            .FindAll(AdminClaimTypes.Tenant)
            .Select(c => c.Value)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .ToList();

        if (!tenantClaims.Contains(tenantId, StringComparer.Ordinal))
        {
            logger.LogWarning(
                "Tenant access denied: requested={TenantId}, authorized=[{AuthorizedTenants}]",
                tenantId,
                string.Join(",", tenantClaims));

            string correlationId = context.HttpContext.Items["CorrelationId"]?.ToString()
                ?? Guid.NewGuid().ToString();

            context.Result = new ObjectResult(new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "Tenant Access Denied",
                Detail = "Not authorized for the requested tenant.",
                Instance = context.HttpContext.Request.Path,
                Extensions = { ["correlationId"] = correlationId },
            })
            {
                StatusCode = StatusCodes.Status403Forbidden,
            };

            return;
        }

        await next().ConfigureAwait(false);
    }
}
