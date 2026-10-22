using Hexalith.EventStore.Middleware;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Hexalith.EventStore.Authorization;

/// <summary>
/// Action filter that enforces tenant claims for direct gateway admin computation endpoints.
/// </summary>
public class AdminTenantAuthorizationFilter(ILogger<AdminTenantAuthorizationFilter> logger) : IAsyncActionFilter {
    /// <summary>
    /// Claim type used to authorize tenant-scoped admin reads.
    /// </summary>
    public const string TenantClaimType = "eventstore:tenant";

    /// <inheritdoc/>
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next) {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        if (GlobalAdministratorHelper.IsGlobalAdministrator(context.HttpContext.User)) {
            _ = await next().ConfigureAwait(false);
            return;
        }

        string? tenantId = ResolveRequestedTenantId(context);
        if (string.IsNullOrWhiteSpace(tenantId)) {
            if (!HasTenantArgument(context)) {
                _ = await next().ConfigureAwait(false);
                return;
            }

            string? firstAuthorizedTenant = GetAuthorizedTenantClaims(context).FirstOrDefault();
            if (string.IsNullOrWhiteSpace(firstAuthorizedTenant)) {
                Deny(context, null, []);
                return;
            }

            context.ActionArguments["tenantId"] = firstAuthorizedTenant;

            _ = await next().ConfigureAwait(false);
            return;
        }

        List<string> tenantClaims = GetAuthorizedTenantClaims(context);
        if (!tenantClaims.Contains(tenantId, StringComparer.Ordinal)) {
            Deny(context, tenantId, tenantClaims);
            return;
        }

        _ = await next().ConfigureAwait(false);
    }

    private static List<string> GetAuthorizedTenantClaims(ActionExecutingContext context)
        => context.HttpContext.User
            .FindAll(TenantClaimType)
            .Select(c => c.Value)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .ToList();

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

    private static bool HasTenantArgument(ActionExecutingContext context)
        => context.ActionArguments.ContainsKey("tenantId")
           || context.ActionDescriptor.Parameters.Any(parameter =>
               string.Equals(parameter.Name, "tenantId", StringComparison.Ordinal));

    private void Deny(ActionExecutingContext context, string? requestedTenantId, IReadOnlyCollection<string> tenantClaims) {
        logger.LogWarning(
            "Gateway admin tenant access denied: requested={TenantId}, authorized=[{AuthorizedTenants}]",
            requestedTenantId ?? "<none>",
            string.Join(",", tenantClaims));

        string correlationId = context.HttpContext.Items[CorrelationIdMiddleware.HttpContextKey]?.ToString()
            ?? "unknown";

        context.Result = new ObjectResult(new ProblemDetails {
            Status = StatusCodes.Status403Forbidden,
            Title = "Tenant Access Denied",
            Detail = "Not authorized for the requested tenant.",
            Instance = context.HttpContext.Request.Path,
            Extensions = { ["correlationId"] = correlationId },
        }) {
            StatusCode = StatusCodes.Status403Forbidden,
        };
    }
}
