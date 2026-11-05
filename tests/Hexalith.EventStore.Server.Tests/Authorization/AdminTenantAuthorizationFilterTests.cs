using System.Reflection;
using System.Security.Claims;

using Hexalith.EventStore.Authorization;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.Controllers;
using Hexalith.EventStore.Middleware;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
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

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task OnActionExecutionAsync_MixedCaseTenant_ForwardsCanonicalTenant(bool useRoute) {
        AdminTenantAuthorizationFilter filter = CreateFilter();
        ActionExecutingContext context = CreateContext(
            CreatePrincipal(new Claim(AdminTenantAuthorizationFilter.TenantClaimType, "TENANT-A")),
            routeTenantId: useRoute ? "Tenant-A" : null,
            queryTenantId: useRoute ? null : "Tenant-A");

        bool called = await InvokeAsync(filter, context);

        called.ShouldBeTrue();
        context.ActionArguments["tenantId"].ShouldBe("tenant-a");
        context.Result.ShouldBeNull();
    }

    [Theory]
    [InlineData(true, "X-Hexalith-Tenant", "tenant-a")]
    [InlineData(true, "X-Hexalith-Tenant", "tenant-b")]
    [InlineData(true, "X-Hexalith-Scope", "platform:tenants")]
    [InlineData(true, "X-Hexalith-Scope", "")]
    [InlineData(false, "X-Hexalith-Tenant", "tenant-a")]
    [InlineData(false, "X-Hexalith-Tenant", "tenant-b")]
    [InlineData(false, "X-Hexalith-Scope", "platform:tenants")]
    [InlineData(false, "X-Hexalith-Scope", "")]
    public async Task OnActionExecutionAsync_AdditionalScopeHeader_ReturnsForbiddenBeforeAction(
        bool useRoute, string headerName, string headerValue) {
        AdminTenantAuthorizationFilter filter = CreateFilter();
        ActionExecutingContext context = CreateContext(
            CreatePrincipal(new Claim(AdminTenantAuthorizationFilter.TenantClaimType, "tenant-a")),
            routeTenantId: useRoute ? "tenant-a" : null,
            queryTenantId: useRoute ? null : "tenant-a");
        context.HttpContext.Request.Headers[headerName] = headerValue;

        bool called = await InvokeAsync(filter, context);

        called.ShouldBeFalse();
        ObjectResult result = context.Result.ShouldBeOfType<ObjectResult>();
        result.StatusCode.ShouldBe(StatusCodes.Status403Forbidden);
        result.Value.ShouldBeOfType<ProblemDetails>().Extensions["correlationId"].ShouldBe("corr-1");
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
    public async Task OnActionExecutionAsync_UnfilteredTenantArgument_RequiresExplicitTenant() {
        AdminTenantAuthorizationFilter filter = CreateFilter();
        ActionExecutingContext context = CreateContext(
            CreatePrincipal(
                new Claim(AdminTenantAuthorizationFilter.TenantClaimType, "tenant-a"),
                new Claim(AdminTenantAuthorizationFilter.TenantClaimType, "tenant-b")),
            includeTenantArgument: true);

        bool called = await InvokeAsync(filter, context);

        called.ShouldBeFalse();
        context.Result.ShouldBeOfType<ObjectResult>().StatusCode.ShouldBe(StatusCodes.Status403Forbidden);
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
    public async Task OnActionExecutionAsync_GlobalAdmin_StillRequiresExplicitTenant() {
        AdminTenantAuthorizationFilter filter = CreateFilter();
        ActionExecutingContext context = CreateContext(
            CreatePrincipal(new Claim("global_admin", "true")),
            includeTenantArgument: true);

        bool called = await InvokeAsync(filter, context);

        called.ShouldBeFalse();
        context.Result.ShouldBeOfType<ObjectResult>().StatusCode.ShouldBe(StatusCodes.Status403Forbidden);
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

    [Theory]
    [InlineData("admin:read", true)]
    [InlineData("other:read", false)]
    [InlineData(null, false)]
    public async Task OnActionExecutionAsync_CatalogedCommandList_RequiresReadPermission(string? permission, bool allowed) {
        ActionExecutingContext context = CreatePlatformContext(permission is null ? [] : [new Claim("eventstore:permission", permission)]);

        bool called = await InvokeAsync(CreateFilter(), context);

        called.ShouldBe(allowed);
        if (allowed) {
            context.HttpContext.Items[AdminPlatformScopeCatalog.ValidatedContextKey].ShouldBe(AdminPlatformScopeCatalog.Namespace);
            context.ActionArguments["tenantId"].ShouldBeNull();
        }
        else {
            context.Result.ShouldBeOfType<ObjectResult>().StatusCode.ShouldBe(StatusCodes.Status403Forbidden);
        }
    }

    [Theory]
    [InlineData("route")]
    [InlineData("query")]
    [InlineData("header")]
    [InlineData("argument")]
    public async Task OnActionExecutionAsync_PlatformWithTenant_ReturnsForbiddenBeforeAction(string source) {
        ActionExecutingContext context = CreatePlatformContext(new Claim("eventstore:permission", AdminPlatformScopeCatalog.ReadPermission));
        switch (source) {
            case "route": context.RouteData.Values["tenantId"] = "tenant-a"; break;
            case "query": context.HttpContext.Request.QueryString = QueryString.Create("tenantId", "tenant-a"); break;
            case "header": context.HttpContext.Request.Headers["X-Hexalith-Tenant"] = "tenant-a"; break;
            case "argument": context.ActionArguments["tenantId"] = "tenant-a"; break;
        }

        bool called = await InvokeAsync(CreateFilter(), context);

        called.ShouldBeFalse();
        context.Result.ShouldBeOfType<ObjectResult>().StatusCode.ShouldBe(StatusCodes.Status403Forbidden);
        context.HttpContext.Items.ContainsKey(AdminPlatformScopeCatalog.ValidatedContextKey).ShouldBeFalse();
    }

    [Theory]
    [InlineData("platform:tenants")]
    [InlineData("platform:Eventstore-admin")]
    [InlineData("")]
    public async Task OnActionExecutionAsync_WrongPlatformScope_ReturnsForbidden(string scope) {
        ActionExecutingContext context = CreatePlatformContext(new Claim("eventstore:permission", AdminPlatformScopeCatalog.ReadPermission));
        context.HttpContext.Request.Headers["X-Hexalith-Scope"] = scope;

        bool called = await InvokeAsync(CreateFilter(), context);

        called.ShouldBeFalse();
        context.Result.ShouldBeOfType<ObjectResult>().StatusCode.ShouldBe(StatusCodes.Status403Forbidden);
    }

    [Theory]
    [InlineData("scope")]
    [InlineData("permission")]
    [InlineData("grant")]
    public async Task OnActionExecutionAsync_DuplicatePlatformCredentialOrScope_ReturnsForbidden(string duplicate) {
        Claim[] claims = duplicate switch {
            "permission" => [new Claim("eventstore:permission", AdminPlatformScopeCatalog.ReadPermission), new Claim("eventstore:permission", AdminPlatformScopeCatalog.ReadPermission)],
            "grant" => [new Claim("eventstore:permission", AdminPlatformScopeCatalog.ReadPermission), new Claim("eventstore:tenant", "Acme"), new Claim("eventstore:tenant", "acme")],
            _ => [new Claim("eventstore:permission", AdminPlatformScopeCatalog.ReadPermission)],
        };
        ActionExecutingContext context = CreatePlatformContext(claims);
        if (duplicate == "scope") {
            context.HttpContext.Request.Headers["X-Hexalith-Scope"] = new Microsoft.Extensions.Primitives.StringValues(
                [AdminPlatformScopeCatalog.Namespace, AdminPlatformScopeCatalog.Namespace]);
        }

        bool called = await InvokeAsync(CreateFilter(), context);

        called.ShouldBeFalse();
        context.Result.ShouldBeOfType<ObjectResult>().StatusCode.ShouldBe(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task OnActionExecutionAsync_PlatformScopeOnUncatalogedAction_ReturnsForbidden() {
        ActionExecutingContext context = CreatePlatformContext(new Claim("eventstore:permission", AdminPlatformScopeCatalog.ReadPermission));
        context.ActionDescriptor = new ControllerActionDescriptor {
            ControllerTypeInfo = typeof(AdminTraceQueryController).GetTypeInfo(),
            MethodInfo = typeof(AdminTraceQueryController).GetMethod(nameof(AdminTraceQueryController.GetCorrelationTraceMap))!,
        };

        bool called = await InvokeAsync(CreateFilter(), context);

        called.ShouldBeFalse();
        context.Result.ShouldBeOfType<ObjectResult>().StatusCode.ShouldBe(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task OnActionExecutionAsync_UnauthenticatedPlatformRequest_ReturnsForbidden() {
        ActionExecutingContext context = CreatePlatformContext();
        context.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("eventstore:permission", AdminPlatformScopeCatalog.ReadPermission)]));

        bool called = await InvokeAsync(CreateFilter(), context);

        called.ShouldBeFalse();
        context.Result.ShouldBeOfType<ObjectResult>().StatusCode.ShouldBe(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task MaintenanceWithoutTenantArgument_RequiresExactSeparateAdmission() {
        ActionExecutingContext context = CreateMaintenanceContext();
        (await InvokeAsync(CreateFilter(), context)).ShouldBeTrue();
        context.Result.ShouldBeNull();
        context.HttpContext.Items[ProjectionDeliveryMaintenanceScopeCatalog.ValidatedContextKey]
            .ShouldBe(ProjectionDeliveryMaintenanceScopeCatalog.Namespace);
        context.HttpContext.Items.ContainsKey(AdminPlatformScopeCatalog.ValidatedContextKey).ShouldBeFalse();
        AdminPlatformScopeCatalog.ForAction(typeof(AdminProjectionRebuildController).FullName!,
            nameof(AdminProjectionRebuildController.ReconcileGlobalAdministratorsDeliveryFromEventStoreAsync)).ShouldBeNull();
        ProjectionDeliveryMaintenanceScopeCatalog.ForAction(typeof(AdminProjectionRebuildController).FullName!,
            nameof(AdminProjectionRebuildController.ReconcileDeliveryFromEventStoreAsync)).ShouldBeNull();
    }

    [Theory]
    [InlineData("missing-scope")]
    [InlineData("duplicate-scope")]
    [InlineData("wrong-scope")]
    [InlineData("read-scope")]
    [InlineData("missing-permission")]
    [InlineData("duplicate-permission")]
    [InlineData("read-permission")]
    [InlineData("foreign-permission")]
    [InlineData("route")]
    [InlineData("query")]
    [InlineData("header")]
    [InlineData("argument")]
    [InlineData("reserved-grant")]
    [InlineData("duplicate-grant")]
    [InlineData("unauthenticated")]
    public async Task MaintenanceInvalidAdmission_DeniesBeforeAction(string invalid) {
        ActionExecutingContext context = CreateMaintenanceContext();
        switch (invalid) {
            case "missing-scope": context.HttpContext.Request.Headers.Remove("X-Hexalith-Scope"); break;
            case "duplicate-scope": context.HttpContext.Request.Headers["X-Hexalith-Scope"] = new Microsoft.Extensions.Primitives.StringValues(
                [ProjectionDeliveryMaintenanceScopeCatalog.Namespace, ProjectionDeliveryMaintenanceScopeCatalog.Namespace]); break;
            case "wrong-scope": context.HttpContext.Request.Headers["X-Hexalith-Scope"] = "platform:global-administrators"; break;
            case "read-scope": context.HttpContext.Request.Headers["X-Hexalith-Scope"] = AdminPlatformScopeCatalog.Namespace; break;
            case "route": context.RouteData.Values["tenantId"] = "tenant-a"; break;
            case "query": context.HttpContext.Request.QueryString = QueryString.Create("tenantId", "tenant-a"); break;
            case "header": context.HttpContext.Request.Headers["X-Hexalith-Tenant"] = "tenant-a"; break;
            case "argument": context.ActionArguments["tenantId"] = null; break;
            default:
                Claim[] claims = invalid switch {
                    "missing-permission" => [],
                    "read-permission" => [new Claim("eventstore:permission", AdminPlatformScopeCatalog.ReadPermission)],
                    "foreign-permission" => [new Claim("eventstore:permission", ProjectionDeliveryMaintenanceScopeCatalog.Permission + ":other")],
                    "duplicate-permission" => [new Claim("eventstore:permission", ProjectionDeliveryMaintenanceScopeCatalog.Permission),
                        new Claim("eventstore:permission", ProjectionDeliveryMaintenanceScopeCatalog.Permission)],
                    "reserved-grant" => [new Claim("eventstore:permission", ProjectionDeliveryMaintenanceScopeCatalog.Permission), new Claim("eventstore:tenant", "system")],
                    "duplicate-grant" => [new Claim("eventstore:permission", ProjectionDeliveryMaintenanceScopeCatalog.Permission),
                        new Claim("eventstore:tenant", "Acme"), new Claim("eventstore:tenant", "acme")],
                    _ => [new Claim("eventstore:permission", ProjectionDeliveryMaintenanceScopeCatalog.Permission)],
                };
                context.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims,
                    invalid == "unauthenticated" ? null : "Test"));
                break;
        }
        (await InvokeAsync(CreateFilter(), context)).ShouldBeFalse();
        context.Result.ShouldBeOfType<ObjectResult>().StatusCode.ShouldBe(StatusCodes.Status403Forbidden);
        context.HttpContext.Items.ContainsKey(ProjectionDeliveryMaintenanceScopeCatalog.ValidatedContextKey).ShouldBeFalse();
    }

    private static ActionExecutingContext CreateMaintenanceContext() {
        ActionExecutingContext context = CreateContext(CreatePrincipal(
            new Claim("eventstore:permission", ProjectionDeliveryMaintenanceScopeCatalog.Permission),
            new Claim("eventstore:tenant", "tenant-a")));
        context.ActionDescriptor = new ControllerActionDescriptor {
            ControllerTypeInfo = typeof(AdminProjectionRebuildController).GetTypeInfo(),
            MethodInfo = typeof(AdminProjectionRebuildController).GetMethod(
                nameof(AdminProjectionRebuildController.ReconcileGlobalAdministratorsDeliveryFromEventStoreAsync))!,
        };
        context.HttpContext.Request.Headers["X-Hexalith-Scope"] = ProjectionDeliveryMaintenanceScopeCatalog.Namespace;
        return context;
    }

    private static ActionExecutingContext CreatePlatformContext(params Claim[] claims) {
        ActionExecutingContext context = CreateContext(CreatePrincipal(claims), includeTenantArgument: true);
        context.ActionDescriptor = new ControllerActionDescriptor {
            ControllerTypeInfo = typeof(AdminCommandsQueryController).GetTypeInfo(),
            MethodInfo = typeof(AdminCommandsQueryController).GetMethod(nameof(AdminCommandsQueryController.GetRecentCommands))!,
        };
        context.HttpContext.Request.Headers["X-Hexalith-Scope"] = AdminPlatformScopeCatalog.Namespace;
        return context;
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
        if (includeTenantArgument || routeTenantId is not null || queryTenantId is not null) {
            actionArguments["tenantId"] = routeTenantId ?? queryTenantId;
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
