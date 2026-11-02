using Hexalith.EventStore.Middleware;
using Hexalith.EventStore.Contracts.Identity;

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

        if (!HasTenantArgument(context)) {
            _ = await next().ConfigureAwait(false);
            return;
        }

        List<string> tenantClaims = GetAuthorizedTenantClaims(context);
        if (!TryResolveRequestedTenantId(context, out string tenantId)
            || !TenantCanonicalizer.TryCanonicalizeGrants(tenantClaims, out IReadOnlySet<string> canonicalGrants)
            || !canonicalGrants.Contains(tenantId)) {
            Deny(context, tenantId, tenantClaims);
            return;
        }

        context.ActionArguments["tenantId"] = tenantId;
        _ = await next().ConfigureAwait(false);
    }

    private static List<string> GetAuthorizedTenantClaims(ActionExecutingContext context)
        => context.HttpContext.User
            .FindAll(TenantClaimType)
            .Select(c => c.Value)
            .ToList();

    private static bool TryResolveRequestedTenantId(ActionExecutingContext context, out string tenantId) {
        bool hasRoute = context.RouteData.Values.TryGetValue("tenantId", out object? routeValue);
        bool hasQuery = context.HttpContext.Request.Query.TryGetValue("tenantId", out Microsoft.Extensions.Primitives.StringValues queryValue);
        string? requested = hasRoute ? routeValue?.ToString()
            : hasQuery && queryValue.Count == 1 ? queryValue[0]
            : !hasQuery && context.ActionArguments.TryGetValue("tenantId", out object? actionValue) ? actionValue?.ToString() : null;
        if (hasRoute && hasQuery || hasQuery && queryValue.Count != 1)
        {
            tenantId = string.Empty;
            return false;
        }

        return TenantCanonicalizer.TryCanonicalize(requested, out tenantId);
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
