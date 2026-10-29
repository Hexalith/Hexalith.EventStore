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

public sealed class GeneratedCatalogDeclarationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GeneratedActionComparesFullCompiledDeclarationIncludingMessageIdVersion(bool drift)
    {
        var fixture = new RouteCatalogTestFixture();
        var envelope = fixture.Envelope;
        if (drift)
        {
            var changed = envelope.Declarations.Single(d => d.Domain == "counter" && d.MessageType == "increment-counter") with { MessageIdVersion = 2 };
            envelope = RouteCatalogCodec.Seal(envelope with
            {
                Declarations = envelope.Declarations.Select(d => d.RouteId == changed.RouteId ? changed : d).ToArray(),
                Routes = envelope.Routes.Select(r => r.RouteId == changed.RouteId ? r with { DeclarationDigest = RouteCatalogCodec.ComputeDeclarationDigest(changed) } : r).ToArray(),
            });
        }
        var snapshot = new CommittedRouteCatalogSnapshot(RouteCatalogCodec.Encode(envelope), RouteCatalogCodec.ComputeRootDigest(envelope), 1);
        IRouteCatalogAuthority authority = Substitute.For<IRouteCatalogAuthority>();
        authority.AcquireAsync(Arg.Any<CancellationToken>()).Returns(_ => new RouteCatalogDispatchLease(snapshot, () => Task.CompletedTask));
        var action = new ActionDescriptor { EndpointMetadata = [new RouteCatalogEndpointAttribute(typeof(IncrementCounter), "command")] };
        var actionContext = new ActionContext(new DefaultHttpContext(), new RouteData(), action);
        actionContext.RouteData.Values["tenant"] = "tenant-a";
        var executing = new ActionExecutingContext(actionContext, [], new Dictionary<string, object?>(), new object());
        bool gatewayCalled = false;
        ActionExecutedContext? completed = null;
        await new GeneratedRouteCatalogFilter(authority).OnActionExecutionAsync(executing, () =>
        {
            if (GeneratedRouteCatalogFilter.ValidateTenant(actionContext.HttpContext, typeof(IncrementCounter), "command", "tenant-a")) gatewayCalled = true;
            completed = new ActionExecutedContext(actionContext, [], executing.Controller);
            return Task.FromResult(completed);
        });
        gatewayCalled.ShouldBe(!drift);
        if (drift) completed!.Result.ShouldBeOfType<StatusCodeResult>().StatusCode.ShouldBe(503);
    }
    [Theory]
    [InlineData("tenant-a", "tenant-a", true)]
    [InlineData("system", "system", true)]
    [InlineData("tenant-a", "tenant-b", false)]
    public async Task ExactCatalogTenantComesOnlyFromTheGeneratedAction(string exactTenant, string partitionTenant, bool accepted)
    {
        var fixture = new RouteCatalogTestFixture();
        var envelope = RouteCatalogCodec.Seal(fixture.Envelope with
        {
            Routes = fixture.Envelope.Routes.Select(r => r.RouteId == "counter-increment-counter" ? r with { ResolverTenantPattern = exactTenant } : r).ToArray(),
        });
        var snapshot = new CommittedRouteCatalogSnapshot(RouteCatalogCodec.Encode(envelope), RouteCatalogCodec.ComputeRootDigest(envelope), 1);
        IRouteCatalogAuthority authority = Substitute.For<IRouteCatalogAuthority>();
        authority.AcquireAsync(Arg.Any<CancellationToken>()).Returns(_ => new RouteCatalogDispatchLease(snapshot, () => Task.CompletedTask));
        var action = new ActionDescriptor { EndpointMetadata = [new RouteCatalogEndpointAttribute(typeof(IncrementCounter), "command")] };
        var actionContext = new ActionContext(new DefaultHttpContext(), new RouteData(), action);
        actionContext.RouteData.Values["tenantId"] = "domain-aggregate-identifier";
        var executing = new ActionExecutingContext(actionContext, [], new Dictionary<string, object?>(), new object());
        bool effects = false;
        ActionExecutedContext? completed = null;
        await new GeneratedRouteCatalogFilter(authority).OnActionExecutionAsync(executing, () =>
        {
            effects = RouteCatalogGeneratedAction.ValidateTenant(actionContext.HttpContext.Items, typeof(IncrementCounter), "command", partitionTenant);
            completed = new ActionExecutedContext(actionContext, [], executing.Controller);
            return Task.FromResult(completed);
        });
        effects.ShouldBe(accepted);
        if (!accepted) completed!.Result.ShouldBeOfType<StatusCodeResult>().StatusCode.ShouldBe(503);
    }
}
