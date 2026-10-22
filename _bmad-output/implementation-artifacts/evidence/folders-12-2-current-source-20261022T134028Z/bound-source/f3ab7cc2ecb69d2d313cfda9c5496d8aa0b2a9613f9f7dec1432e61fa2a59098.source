using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;

namespace Hexalith.EventStore.Admin.Server.Authorization;

/// <summary>
/// Action filter that validates the caller's <c>eventstore:tenant</c> claims against
/// the requested tenant ID from route or query parameters (SEC-3 tenant isolation).
/// </summary>
public class AdminTenantAuthorizationFilter(ILogger<AdminTenantAuthorizationFilter> logger) : IAsyncActionFilter {
    /// <inheritdoc/>
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next) {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        if (context.HttpContext.User.HasClaim(AdminClaimTypes.AdminRole, "Admin")) {
            _ = await next().ConfigureAwait(false);
            return;
        }

        string? tenantId = ResolveRequestedTenantId(context);
        List<string> tenantClaims = GetAuthorizedTenantClaims(context);

        if (string.IsNullOrWhiteSpace(tenantId)) {
            if (!HasTenantArgument(context)) {
                _ = await next().ConfigureAwait(false);
                return;
            }

            string? firstAuthorizedTenant = tenantClaims.FirstOrDefault();
            if (string.IsNullOrWhiteSpace(firstAuthorizedTenant)) {
                Deny(context);
                return;
            }

            context.ActionArguments["tenantId"] = firstAuthorizedTenant;
            _ = await next().ConfigureAwait(false);
            return;
        }

        if (!tenantClaims.Contains(tenantId, StringComparer.Ordinal)) {
            Deny(context);
            return;
        }

        _ = await next().ConfigureAwait(false);
    }

    private static List<string> GetAuthorizedTenantClaims(ActionExecutingContext context)
        => context.HttpContext.User
            .FindAll(AdminClaimTypes.Tenant)
            .Select(claim => claim.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToList();

    private static bool HasTenantArgument(ActionExecutingContext context)
        => context.ActionArguments.ContainsKey("tenantId")
            || context.ActionDescriptor.Parameters.Any(parameter =>
                string.Equals(parameter.Name, "tenantId", StringComparison.Ordinal));

    private static string? ResolveRequestedTenantId(ActionExecutingContext context) {
        if (context.RouteData.Values.TryGetValue("tenantId", out object? routeValue)
            && !string.IsNullOrWhiteSpace(routeValue?.ToString())) {
            return routeValue.ToString();
        }

        if (context.HttpContext.Request.Query.TryGetValue("tenantId", out Microsoft.Extensions.Primitives.StringValues queryValue)
            && !string.IsNullOrWhiteSpace(queryValue.ToString())) {
            return queryValue.ToString();
        }

        if (context.ActionArguments.TryGetValue("tenantId", out object? actionValue)
            && !string.IsNullOrWhiteSpace(actionValue?.ToString())) {
            return actionValue.ToString();
        }

        return null;
    }

    private void Deny(ActionExecutingContext context) {
        string correlationId = context.HttpContext.Items["CorrelationId"]?.ToString() ?? "unknown";
        logger.LogWarning("Admin tenant access denied. CorrelationId={CorrelationId}", correlationId);

        context.Result = new ObjectResult(new ProblemDetails {
            Status = StatusCodes.Status403Forbidden,
            Title = "Forbidden",
            Detail = "The request is not authorized for the requested scope.",
            Extensions = { ["correlationId"] = correlationId },
        }) {
            StatusCode = StatusCodes.Status403Forbidden,
        };
    }
}
