using System.Security.Claims;

using Hexalith.EventStore.Contracts.Rest;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.Sample.Counter.Commands;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Verifies generated scope admission happens before catalog state access.</summary>
public sealed class GeneratedCatalogScopePreflightTests
{
    /// <summary>Rejects locally invalid scope and grant combinations without acquiring authority or running the action.</summary>
    /// <param name="source">The generated action's effective scope source.</param>
    /// <param name="scenario">The invalid request or grant mutation.</param>
    /// <param name="expected">The bounded denial status.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Theory]
    [InlineData(RestTenantSource.Route, "missing", 400)]
    [InlineData(RestTenantSource.Route, "malformed", 400)]
    [InlineData(RestTenantSource.Route, "reserved", 400)]
    [InlineData(RestTenantSource.Route, "duplicate-route", 400)]
    [InlineData(RestTenantSource.Route, "tenant-header", 400)]
    [InlineData(RestTenantSource.Route, "platform-header", 400)]
    [InlineData(RestTenantSource.Route, "malformed-grant", 403)]
    [InlineData(RestTenantSource.Route, "reserved-grant", 403)]
    [InlineData(RestTenantSource.Route, "duplicate-grant", 403)]
    [InlineData(RestTenantSource.Route, "unauthorized", 403)]
    [InlineData(RestTenantSource.Route, "anonymous", 403)]
    [InlineData(RestTenantSource.Claims, "missing", 400)]
    [InlineData(RestTenantSource.Claims, "malformed", 400)]
    [InlineData(RestTenantSource.Claims, "reserved", 400)]
    [InlineData(RestTenantSource.Claims, "duplicate-header", 400)]
    [InlineData(RestTenantSource.Claims, "platform-header", 400)]
    [InlineData(RestTenantSource.Claims, "malformed-grant", 403)]
    [InlineData(RestTenantSource.Claims, "reserved-grant", 403)]
    [InlineData(RestTenantSource.Claims, "duplicate-grant", 403)]
    [InlineData(RestTenantSource.Claims, "unauthorized", 403)]
    [InlineData(RestTenantSource.Claims, "anonymous", 403)]
    [InlineData(RestTenantSource.Platform, "missing", 400)]
    [InlineData(RestTenantSource.Platform, "malformed", 400)]
    [InlineData(RestTenantSource.Platform, "duplicate-header", 400)]
    [InlineData(RestTenantSource.Platform, "tenant-header", 400)]
    [InlineData(RestTenantSource.Platform, "malformed-grant", 403)]
    [InlineData(RestTenantSource.Platform, "reserved-grant", 403)]
    [InlineData(RestTenantSource.Platform, "duplicate-grant", 403)]
    [InlineData(RestTenantSource.Platform, "anonymous", 403)]
    [InlineData(RestTenantSource.System, "reserved", 400)]
    public async Task InvalidScopeOrGrantsNeverAcquireCatalogAuthority(RestTenantSource source, string scenario, int expected)
    {
        IRouteCatalogAuthority authority = Substitute.For<IRouteCatalogAuthority>();
        authority.AcquireAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException<RouteCatalogDispatchLease>(new RouteCatalogUnavailableException("Unavailable")));
        ActionExecutingContext context = CreateContext(source, scenario);
        bool actionCalled = false;

        await new GeneratedRouteCatalogFilter(authority).OnActionExecutionAsync(context, () =>
        {
            actionCalled = true;
            return Task.FromResult(new ActionExecutedContext(context, [], context.Controller));
        });

        authority.ReceivedCalls().ShouldBeEmpty();
        actionCalled.ShouldBeFalse();
        ObjectResult result = context.Result.ShouldBeOfType<ObjectResult>();
        result.StatusCode.ShouldBe(expected);
        result.Value.ShouldBeOfType<ProblemDetails>().Status.ShouldBe(expected);
        result.ContentTypes.ShouldContain("application/problem+json");
    }

    /// <summary>Retains catalog authorization for valid explicit scopes.</summary>
    /// <param name="source">The generated action's effective scope source.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Theory]
    [InlineData(RestTenantSource.Route)]
    [InlineData(RestTenantSource.Claims)]
    [InlineData(RestTenantSource.Platform)]
    public async Task CanonicalExplicitScopeStillUsesCatalogAuthority(RestTenantSource source)
    {
        IRouteCatalogAuthority authority = Substitute.For<IRouteCatalogAuthority>();
        // Admission must reach the existing authority even when it is unavailable; a local
        // scope check never substitutes for catalog declaration or operation authorization.
        authority.AcquireAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException<RouteCatalogDispatchLease>(new RouteCatalogUnavailableException("Unavailable")));
        ActionExecutingContext context = CreateContext(source, "valid");

        await new GeneratedRouteCatalogFilter(authority).OnActionExecutionAsync(context,
            () => throw new InvalidOperationException("Unavailable authority must refuse before action execution."));

        await authority.Received(1).AcquireAsync(Arg.Any<CancellationToken>());
        context.Result.ShouldBeOfType<StatusCodeResult>().StatusCode.ShouldBe(503);
    }

    private static ActionExecutingContext CreateContext(RestTenantSource source, string scenario)
    {
        var http = new DefaultHttpContext();
        var routes = new RouteData();
        var claims = new List<Claim> { new("eventstore:tenant", "ACME"), new("eventstore:tenant", "other") };
        if (source == RestTenantSource.Route)
            routes.Values["tenantId"] = "AcMe";
        else if (source == RestTenantSource.Claims)
            http.Request.Headers["X-Hexalith-Tenant"] = "AcMe";
        else if (source == RestTenantSource.Platform)
        {
            http.Request.Headers["X-Hexalith-Scope"] = "platform:tenants";
            // This route value is a domain aggregate identifier, not a request scope.
            routes.Values["tenantId"] = "system";
        }

        switch (scenario)
        {
            case "missing":
                routes.Values.Clear();
                http.Request.Headers.Clear();
                break;
            case "malformed":
            case "reserved":
                string invalid = scenario == "reserved" ? "SYSTEM" : " acme";
                if (source == RestTenantSource.Route) routes.Values["tenantId"] = invalid;
                else http.Request.Headers[source == RestTenantSource.Platform ? "X-Hexalith-Scope" : "X-Hexalith-Tenant"] = invalid;
                break;
            case "duplicate-route": routes.Values["tenant"] = "acme"; break;
            case "duplicate-header":
                http.Request.Headers[source == RestTenantSource.Platform ? "X-Hexalith-Scope" : "X-Hexalith-Tenant"] = new[] { "acme", "acme" };
                break;
            case "tenant-header": http.Request.Headers["X-Hexalith-Tenant"] = "acme"; break;
            case "platform-header": http.Request.Headers["X-Hexalith-Scope"] = "platform:tenants"; break;
            case "malformed-grant": claims.Add(new("eventstore:tenant", " acme")); break;
            case "reserved-grant": claims.Add(new("eventstore:tenant", "SYSTEM")); break;
            case "duplicate-grant": claims.Add(new("eventstore:tenant", "acme")); break;
            case "unauthorized": claims = [new("eventstore:tenant", "other")]; break;
        }

        http.User = new ClaimsPrincipal(new ClaimsIdentity(claims, scenario == "anonymous" ? null : "test"));
        var action = new ActionDescriptor
        {
            EndpointMetadata = [new RouteCatalogEndpointAttribute(typeof(IncrementCounter), "command") { TenantSource = source }],
        };
        return new ActionExecutingContext(new ActionContext(http, routes, action), [], new Dictionary<string, object?>(), new object());
    }
}
