using System.Security.Claims;

using Hexalith.EventStore.Authorization;
using Hexalith.EventStore.Middleware;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Authorization;

public class AdminTenantAuthorizationFilterTests {
    [Fact]
    public async Task OnActionExecutionAsync_MatchingRouteTenant_CallsNext() {
        AdminTenantAuthorizationFilter filter = CreateFilter();
        ActionExecutingContext context = CreateContext(
            CreatePrincipal(new Claim(AdminTenantAuthorizationFilter.TenantClaimType, "tenant-a")),
            routeTenantId: "tenant-a");

        bool called = await InvokeAsync(filter, context);

        called.ShouldBeTrue();
        context.Result.ShouldBeNull();
    }

    [Fact]
    public async Task OnActionExecutionAsync_BlockedRouteTenant_ReturnsForbidden() {
        AdminTenantAuthorizationFilter filter = CreateFilter();
        ActionExecutingContext context = CreateContext(
            CreatePrincipal(new Claim(AdminTenantAuthorizationFilter.TenantClaimType, "tenant-a")),
            routeTenantId: "tenant-b");

        bool called = await InvokeAsync(filter, context);

        called.ShouldBeFalse();
        ObjectResult result = context.Result.ShouldBeOfType<ObjectResult>();
        result.StatusCode.ShouldBe(StatusCodes.Status403Forbidden);
        ProblemDetails problem = result.Value.ShouldBeOfType<ProblemDetails>();
        problem.Extensions["correlationId"].ShouldBe("corr-1");
    }

    [Fact]
    public async Task OnActionExecutionAsync_BlockedQueryTenant_ReturnsForbidden() {
        AdminTenantAuthorizationFilter filter = CreateFilter();
        ActionExecutingContext context = CreateContext(
            CreatePrincipal(new Claim(AdminTenantAuthorizationFilter.TenantClaimType, "tenant-a")),
            queryTenantId: "tenant-b");

        bool called = await InvokeAsync(filter, context);

        called.ShouldBeFalse();
        ObjectResult result = context.Result.ShouldBeOfType<ObjectResult>();
        result.StatusCode.ShouldBe(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task OnActionExecutionAsync_UnfilteredTenantArgument_NarrowsToFirstTenantClaim() {
        AdminTenantAuthorizationFilter filter = CreateFilter();
        ActionExecutingContext context = CreateContext(
            CreatePrincipal(
                new Claim(AdminTenantAuthorizationFilter.TenantClaimType, "tenant-a"),
                new Claim(AdminTenantAuthorizationFilter.TenantClaimType, "tenant-b")),
            includeTenantArgument: true);

        bool called = await InvokeAsync(filter, context);

        called.ShouldBeTrue();
        context.Result.ShouldBeNull();
        context.ActionArguments["tenantId"].ShouldBe("tenant-a");
    }

    [Fact]
    public async Task OnActionExecutionAsync_UnfilteredTenantArgumentWithoutTenantClaim_ReturnsForbidden() {
        AdminTenantAuthorizationFilter filter = CreateFilter();
        ActionExecutingContext context = CreateContext(
            CreatePrincipal(new Claim("sub", "readonly-user")),
            includeTenantArgument: true);

        bool called = await InvokeAsync(filter, context);

        called.ShouldBeFalse();
        ObjectResult result = context.Result.ShouldBeOfType<ObjectResult>();
        result.StatusCode.ShouldBe(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task OnActionExecutionAsync_GlobalAdmin_DoesNotNarrowUnfilteredTenantArgument() {
        AdminTenantAuthorizationFilter filter = CreateFilter();
        ActionExecutingContext context = CreateContext(
            CreatePrincipal(new Claim("global_admin", "true")),
            includeTenantArgument: true);

        bool called = await InvokeAsync(filter, context);

        called.ShouldBeTrue();
        context.Result.ShouldBeNull();
        context.ActionArguments["tenantId"].ShouldBeNull();
    }

    [Fact]
    public async Task OnActionExecutionAsync_NoTenantInRequestOrAction_CallsNext() {
        AdminTenantAuthorizationFilter filter = CreateFilter();
        ActionExecutingContext context = CreateContext(
            CreatePrincipal(new Claim("sub", "readonly-user")));

        bool called = await InvokeAsync(filter, context);

        called.ShouldBeTrue();
        context.Result.ShouldBeNull();
    }

    private static AdminTenantAuthorizationFilter CreateFilter()
        => new(NullLogger<AdminTenantAuthorizationFilter>.Instance);

    private static ClaimsPrincipal CreatePrincipal(params Claim[] claims)
        => new(new ClaimsIdentity(claims, "Test"));

    private static ActionExecutingContext CreateContext(
        ClaimsPrincipal user,
        string? routeTenantId = null,
        string? queryTenantId = null,
        bool includeTenantArgument = false) {
        var httpContext = new DefaultHttpContext {
            User = user,
        };
        httpContext.Items[CorrelationIdMiddleware.HttpContextKey] = "corr-1";

        if (!string.IsNullOrWhiteSpace(queryTenantId)) {
            httpContext.Request.QueryString = QueryString.Create("tenantId", queryTenantId);
        }

        var actionContext = new ActionContext(
            httpContext,
            new RouteData(),
            new ActionDescriptor());

        if (!string.IsNullOrWhiteSpace(routeTenantId)) {
            actionContext.RouteData.Values["tenantId"] = routeTenantId;
        }

        var actionArguments = new Dictionary<string, object?>();
        if (includeTenantArgument) {
            actionArguments["tenantId"] = null;
        }

        return new ActionExecutingContext(
            actionContext,
            [],
            actionArguments,
            controller: new object());
    }

    private static async Task<bool> InvokeAsync(AdminTenantAuthorizationFilter filter, ActionExecutingContext context) {
        bool called = false;
        ActionExecutionDelegate next = () => {
            called = true;
            var actionContext = new ActionContext(
                context.HttpContext,
                context.RouteData,
                context.ActionDescriptor,
                context.ModelState);
            return Task.FromResult(new ActionExecutedContext(actionContext, [], context.Controller));
        };

        await filter.OnActionExecutionAsync(context, next).ConfigureAwait(false);
        return called;
    }
}
