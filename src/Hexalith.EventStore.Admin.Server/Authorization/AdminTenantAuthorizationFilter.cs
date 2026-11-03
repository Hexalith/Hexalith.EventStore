using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.Logging;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Routing;

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

        var action = context.ActionDescriptor as ControllerActionDescriptor;
        PlatformAdmissionDeclaration? admission = action is null ? null
            : AdminPlatformScopeCatalog.ForAction(action.ControllerTypeInfo.FullName!, action.MethodInfo.Name);
        bool hasTenantArgument = HasTenantArgument(context);
        if (!hasTenantArgument && admission is null) {
            _ = await next().ConfigureAwait(false);
            return;
        }

        if (context.HttpContext.Request.Headers.TryGetValue("X-Hexalith-Scope", out var scopeHeader)) {
            if (admission is null || scopeHeader.Count != 1
                || !string.Equals(scopeHeader[0], admission.Namespace, StringComparison.Ordinal)
                || context.RouteData.Values.ContainsKey("tenantId")
                || context.HttpContext.Request.Query.ContainsKey("tenantId")
                || context.ActionArguments.TryGetValue("tenantId", out object? tenantArgument) && tenantArgument is not null
                || context.HttpContext.Request.Headers.ContainsKey("X-Hexalith-Tenant")
                || context.HttpContext.User.Identity?.IsAuthenticated != true
                || context.HttpContext.User.FindAll("eventstore:permission").Count(claim =>
                    string.Equals(claim.Value, admission.RequiredHumanPermission, StringComparison.Ordinal)) != 1
                || !TenantCanonicalizer.TryCanonicalizeGrants(GetAuthorizedTenantClaims(context), out _)) {
                Deny(context);
                return;
            }

            context.HttpContext.Items[AdminPlatformScopeCatalog.ValidatedContextKey] = admission.Namespace;
            _ = await next().ConfigureAwait(false);
            return;
        }

        List<string> tenantClaims = GetAuthorizedTenantClaims(context);
        if (!hasTenantArgument
            || context.HttpContext.Request.Headers.ContainsKey("X-Hexalith-Tenant")
            || !TryResolveRequestedTenantId(context, out string tenantId)
            || !TenantCanonicalizer.TryCanonicalizeGrants(tenantClaims, out IReadOnlySet<string> canonicalGrants)
            || !canonicalGrants.Contains(tenantId)) {
            Deny(context);
            return;
        }

        context.ActionArguments["tenantId"] = tenantId;
        _ = await next().ConfigureAwait(false);
    }

    private static List<string> GetAuthorizedTenantClaims(ActionExecutingContext context)
        => context.HttpContext.User
            .FindAll(AdminClaimTypes.Tenant)
            .Select(claim => claim.Value)
            .ToList();

    private static bool HasTenantArgument(ActionExecutingContext context)
        => context.ActionArguments.ContainsKey("tenantId")
            || context.ActionDescriptor.Parameters.Any(parameter =>
                string.Equals(parameter.Name, "tenantId", StringComparison.Ordinal));

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
