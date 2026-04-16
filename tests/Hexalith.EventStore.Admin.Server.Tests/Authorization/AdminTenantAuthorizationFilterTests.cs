using System.Security.Claims;

using Hexalith.EventStore.Admin.Server.Authorization;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging.Abstractions;

namespace Hexalith.EventStore.Admin.Server.Tests.Authorization;

public class AdminTenantAuthorizationFilterTests {
    private readonly AdminTenantAuthorizationFilter _sut = new(NullLogger<AdminTenantAuthorizationFilter>.Instance);

    [Fact]
    public async Task OnActionExecutionAsync_TenantClaimMatchesRoute_Passes() {
        ActionExecutingContext context = CreateContext(
            routeTenantId: "tenant-a",
            tenantClaims: ["tenant-a", "tenant-b"]);
        bool nextCalled = false;

        await _sut.OnActionExecutionAsync(context, () => {
            nextCalled = true;
            return Task.FromResult<ActionExecutedContext>(null!);
        });

        nextCalled.ShouldBeTrue();
        context.Result.ShouldBeNull();
    }

    [Fact]
    public async Task OnActionExecutionAsync_TenantClaimAbsent_Returns403() {
        ActionExecutingContext context = CreateContext(
            routeTenantId: "tenant-a",
            tenantClaims: []);

        await _sut.OnActionExecutionAsync(context, () => Task.FromResult<ActionExecutedContext>(null!));

        _ = context.Result.ShouldNotBeNull();
        ObjectResult objectResult = context.Result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(403);
    }

    [Fact]
    public async Task OnActionExecutionAsync_TenantClaimNotMatching_Returns403() {
        ActionExecutingContext context = CreateContext(
            routeTenantId: "tenant-c",
            tenantClaims: ["tenant-a", "tenant-b"]);

        await _sut.OnActionExecutionAsync(context, () => Task.FromResult<ActionExecutedContext>(null!));

        _ = context.Result.ShouldNotBeNull();
        ObjectResult objectResult = context.Result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(403);
    }

    [Fact]
    public async Task OnActionExecutionAsync_NoTenantIdInRoute_SkipsValidation() {
        ActionExecutingContext context = CreateContext(
            routeTenantId: null,
            tenantClaims: []);
        bool nextCalled = false;

        await _sut.OnActionExecutionAsync(context, () => {
            nextCalled = true;
            return Task.FromResult<ActionExecutedContext>(null!);
        });

        nextCalled.ShouldBeTrue();
        context.Result.ShouldBeNull();
    }

    [Fact]
    public async Task OnActionExecutionAsync_TenantIdInQueryString_Validates() {
        ActionExecutingContext context = CreateContext(
            routeTenantId: null,
            queryTenantId: "tenant-a",
            tenantClaims: ["tenant-b"]);

        await _sut.OnActionExecutionAsync(context, () => Task.FromResult<ActionExecutedContext>(null!));

        _ = context.Result.ShouldNotBeNull();
        ObjectResult objectResult = context.Result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(403);
    }

    private static ActionExecutingContext CreateContext(
        string? routeTenantId,
        string[] tenantClaims,
        string? queryTenantId = null) {
        var claims = new List<Claim>();
        foreach (string tenant in tenantClaims) {
            claims.Add(new Claim(AdminClaimTypes.Tenant, tenant));
        }

        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext { User = principal };

        if (queryTenantId is not null) {
            httpContext.Request.QueryString = new QueryString($"?tenantId={queryTenantId}");
        }

        var routeData = new RouteData();
        if (routeTenantId is not null) {
            routeData.Values["tenantId"] = routeTenantId;
        }

        var actionContext = new ActionContext(httpContext, routeData, new ActionDescriptor());
        return new ActionExecutingContext(
            actionContext,
            [],
            new Dictionary<string, object?>(),
            controller: null!);
    }
}
