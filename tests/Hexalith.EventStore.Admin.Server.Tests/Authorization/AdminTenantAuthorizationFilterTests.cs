using System.Security.Claims;
using System.Reflection;

using Hexalith.EventStore.Admin.Server.Authorization;
using Hexalith.EventStore.Admin.Server.Controllers;
using Hexalith.EventStore.Admin.Server.Tests.Helpers;
using Hexalith.EventStore.Contracts.Routing;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
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

    [Fact]
    public async Task OnActionExecutionAsync_OmittedTenantArgument_DeniesBeforeNext() {
        ActionExecutingContext context = CreateContext(
            routeTenantId: null,
            tenantClaims: ["tenant-a", "tenant-b"],
            includeTenantArgument: true);
        bool nextCalled = false;

        await _sut.OnActionExecutionAsync(context, () => {
            nextCalled = true;
            return Task.FromResult<ActionExecutedContext>(null!);
        });

        nextCalled.ShouldBeFalse();
        context.Result.ShouldBeOfType<ObjectResult>().StatusCode.ShouldBe(403);
    }

    [Fact]
    public async Task OnActionExecutionAsync_OmittedTenantArgumentWithoutClaim_DeniesBeforeNext() {
        ActionExecutingContext context = CreateContext(
            routeTenantId: null,
            tenantClaims: [],
            includeTenantArgument: true);
        bool nextCalled = false;

        await _sut.OnActionExecutionAsync(context, () => {
            nextCalled = true;
            return Task.FromResult<ActionExecutedContext>(null!);
        });

        nextCalled.ShouldBeFalse();
        context.Result.ShouldBeOfType<ObjectResult>().StatusCode.ShouldBe(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task OnActionExecutionAsync_AdminWithoutTenantClaim_DeniesScope() {
        ActionExecutingContext context = CreateContext(
            routeTenantId: "any-tenant",
            tenantClaims: [],
            admin: true);
        bool nextCalled = false;

        await _sut.OnActionExecutionAsync(context, () => {
            nextCalled = true;
            return Task.FromResult<ActionExecutedContext>(null!);
        });

        nextCalled.ShouldBeFalse();
        context.Result.ShouldBeOfType<ObjectResult>().StatusCode.ShouldBe(403);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OnActionExecutionAsync_AdminWithOmittedTenantArgument_Denies(bool withTenantClaim) {
        ActionExecutingContext context = CreateContext(
            routeTenantId: null,
            tenantClaims: withTenantClaim ? ["tenant-a"] : [],
            includeTenantArgument: true,
            admin: true);
        bool nextCalled = false;

        await _sut.OnActionExecutionAsync(context, () => {
            nextCalled = true;
            return Task.FromResult<ActionExecutedContext>(null!);
        });

        nextCalled.ShouldBeFalse();
        context.Result.ShouldBeOfType<ObjectResult>().StatusCode.ShouldBe(403);
        context.ActionArguments["tenantId"].ShouldBeNull();
    }

    [Theory]
    [InlineData(" ", null, null)]
    [InlineData(null, "", null)]
    [InlineData(null, "%20%20", null)]
    [InlineData(null, null, "")]
    [InlineData(null, null, "  ")]
    public async Task OnActionExecutionAsync_BlankRequestedTenant_Denies(
        string? routeTenantId,
        string? queryTenantId,
        string? actionTenantId) {
        ActionExecutingContext context = CreateContext(
            routeTenantId: routeTenantId,
            tenantClaims: ["tenant-a"],
            queryTenantId: queryTenantId,
            includeTenantArgument: true,
            actionTenantId: actionTenantId);
        bool nextCalled = false;

        await _sut.OnActionExecutionAsync(context, () => {
            nextCalled = true;
            return Task.FromResult<ActionExecutedContext>(null!);
        });

        nextCalled.ShouldBeFalse();
        context.Result.ShouldBeOfType<ObjectResult>().StatusCode.ShouldBe(403);
    }

    [Fact]
    public async Task OnActionExecutionAsync_BlankTenantClaims_Deny() {
        ActionExecutingContext context = CreateContext(
            routeTenantId: null,
            tenantClaims: [" ", "", "tenant-b"],
            includeTenantArgument: true);
        bool nextCalled = false;

        await _sut.OnActionExecutionAsync(context, () => {
            nextCalled = true;
            return Task.FromResult<ActionExecutedContext>(null!);
        });

        nextCalled.ShouldBeFalse();
        context.Result.ShouldBeOfType<ObjectResult>().StatusCode.ShouldBe(403);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("tenant-a")]
    public async Task OnActionExecutionAsync_OnlyBlankTenantClaims_DenyBeforeNext(string? routeTenantId) {
        ActionExecutingContext context = CreateContext(
            routeTenantId: routeTenantId,
            tenantClaims: [" ", ""],
            includeTenantArgument: true);
        bool nextCalled = false;

        await _sut.OnActionExecutionAsync(context, () => {
            nextCalled = true;
            return Task.FromResult<ActionExecutedContext>(null!);
        });

        nextCalled.ShouldBeFalse();
        context.Result.ShouldBeOfType<ObjectResult>().StatusCode.ShouldBe(StatusCodes.Status403Forbidden);
        context.ActionArguments["tenantId"].ShouldBeNull();
    }

    [Fact]
    public async Task OnActionExecutionAsync_BlankRouteAndQuery_DoNotMaskUnauthorizedActionTenant() {
        ActionExecutingContext context = CreateContext(
            routeTenantId: " ",
            tenantClaims: ["tenant-a"],
            queryTenantId: "%20",
            includeTenantArgument: true,
            actionTenantId: "tenant-c");
        bool nextCalled = false;

        await _sut.OnActionExecutionAsync(context, () => {
            nextCalled = true;
            return Task.FromResult<ActionExecutedContext>(null!);
        });

        nextCalled.ShouldBeFalse();
        context.Result.ShouldBeOfType<ObjectResult>().StatusCode.ShouldBe(StatusCodes.Status403Forbidden);
        context.ActionArguments["tenantId"].ShouldBe("tenant-c");
    }

    [Fact]
    public async Task OnActionExecutionAsync_DenialLogAndProblemAreIdentifierFree() {
        var logger = new RecordingLogger<AdminTenantAuthorizationFilter>();
        var filter = new AdminTenantAuthorizationFilter(logger);
        ActionExecutingContext context = CreateContext(
            routeTenantId: "tenant-secret",
            tenantClaims: ["authorized-secret"]);
        context.HttpContext.Items["CorrelationId"] = "correlation-safe";

        await filter.OnActionExecutionAsync(
            context,
            () => Task.FromResult<ActionExecutedContext>(null!));

        ObjectResult result = context.Result.ShouldBeOfType<ObjectResult>();
        string problem = System.Text.Json.JsonSerializer.Serialize(result.Value);
        string log = logger.Records.ShouldHaveSingleItem().Message;
        problem.ShouldNotContain("tenant-secret");
        problem.ShouldNotContain("authorized-secret");
        log.ShouldNotContain("tenant-secret");
        log.ShouldNotContain("authorized-secret");
        log.ShouldContain("correlation-safe");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CatalogedGlobalListRequiresExplicitPlatformScopeAndReadPermission(bool permitted) {
        ActionExecutingContext context = CreateContext(null, []);
        context.ActionDescriptor = new ControllerActionDescriptor {
            ControllerTypeInfo = typeof(AdminStreamsController).GetTypeInfo(),
            MethodInfo = typeof(AdminStreamsController).GetMethod(nameof(AdminStreamsController.GetRecentCommandsAsync))!,
            Parameters = [new ControllerParameterDescriptor { Name = "tenantId" }],
        };
        context.HttpContext.Request.Headers["X-Hexalith-Scope"] = AdminPlatformScopeCatalog.Namespace;
        context.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            permitted ? [new Claim("eventstore:permission", AdminPlatformScopeCatalog.ReadPermission)] : [], "TestAuth"));
        bool nextCalled = false;

        await _sut.OnActionExecutionAsync(context, () => {
            nextCalled = true;
            return Task.FromResult<ActionExecutedContext>(null!);
        });

        nextCalled.ShouldBe(permitted);
        if (permitted) context.HttpContext.Items[AdminPlatformScopeCatalog.ValidatedContextKey].ShouldBe(AdminPlatformScopeCatalog.Namespace);
        else context.Result.ShouldBeOfType<ObjectResult>().StatusCode.ShouldBe(403);
    }

    private static ActionExecutingContext CreateContext(
        string? routeTenantId,
        string[] tenantClaims,
        string? queryTenantId = null,
        bool includeTenantArgument = false,
        bool admin = false,
        string? actionTenantId = null) {
        var claims = new List<Claim>();
        foreach (string tenant in tenantClaims) {
            claims.Add(new Claim(AdminClaimTypes.Tenant, tenant));
        }

        if (admin) {
            claims.Add(new Claim(AdminClaimTypes.AdminRole, "Admin"));
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

        var descriptor = new ActionDescriptor { Parameters = new List<ParameterDescriptor>() };
        if (routeTenantId is not null || queryTenantId is not null || includeTenantArgument)
            descriptor.Parameters.Add(new ParameterDescriptor { Name = "tenantId" });
        var actionContext = new ActionContext(httpContext, routeData, descriptor);
        Dictionary<string, object?> arguments = includeTenantArgument
            ? new Dictionary<string, object?> { ["tenantId"] = actionTenantId }
            : new Dictionary<string, object?>();
        return new ActionExecutingContext(
            actionContext,
            [],
            arguments,
            controller: null!);
    }
}
