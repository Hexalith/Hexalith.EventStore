using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.Sample.Counter.Commands;
using Hexalith.EventStore.Sample.Counter.Queries;
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
    [InlineData("system", "system", false)]
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

    [Fact]
    public void EveryCatalogCommandDeclaresExactlyOneMessageIdVersion()
    {
        // The drift-checked routing catalog is the version manifest; RouteCatalogCodec.Decode validates it.
        RouteContractDeclaration[] commands = [.. new RouteCatalogTestFixture().Envelope.Declarations.Where(d => d.Kind == "command")];

        commands.ShouldNotBeEmpty();
        commands.ShouldAllBe(d => d.MessageIdVersion == 1 || d.MessageIdVersion == 2);
        commands.GroupBy(d => (d.Domain, d.MessageType)).ShouldAllBe(group => group.Count() == 1);
    }

    [Fact]
    public async Task GeneratedCommandPublishesOnlyTheCompiledDeclarationMessageIdVersionForTheAction()
    {
        const string key = RouteCatalogGeneratedAction.MessageIdVersionKey;
        int declared = CompiledRouteCatalogDeclarations.Read([typeof(IncrementCounter).Assembly])
            .Single(d => d.Kind == "command" && d.Domain == "counter" && d.MessageType == "increment-counter").MessageIdVersion
            .ShouldNotBeNull();
        declared.ShouldBe(1);
        var httpContext = new DefaultHttpContext();
        // Caller inputs claim v2, and an earlier pipeline stage pre-seeds v2; neither may select the version.
        httpContext.Request.Headers["X-Hexalith-MessageId-Version"] = "2";
        httpContext.Request.Headers[key] = "2";
        httpContext.Request.QueryString = new QueryString("?messageIdVersion=2");
        httpContext.Items[key] = 2;
        (ActionExecutingContext executing, ActionContext actionContext) = CreateExecution(httpContext, typeof(IncrementCounter), "command");
        bool presentBeforeValidation = true;
        bool validated = false;
        object? published = null;
        bool legacySelected = false;
        string? statusMessageId = null;
        ActionExecutedContext? completed = null;

        await new GeneratedRouteCatalogFilter(CreateAuthority()).OnActionExecutionAsync(executing, () =>
        {
            presentBeforeValidation = httpContext.Items.ContainsKey(key);
            validated = RouteCatalogGeneratedAction.ValidateTenant(httpContext.Items, typeof(IncrementCounter), "command", "tenant-a");
            published = httpContext.Items.TryGetValue(key, out object? value) ? value : null;
            // A v1-only id is selected because the declaration, not the caller's v2 claim, decides.
            legacySelected = RouteCatalogGeneratedAction.TrySelectStatusMessageId(httpContext.Items, "legacy-status-1", out statusMessageId);
            completed = new ActionExecutedContext(actionContext, [], executing.Controller);
            return Task.FromResult(completed);
        });

        presentBeforeValidation.ShouldBeFalse();
        validated.ShouldBeTrue();
        published.ShouldBeOfType<int>().ShouldBe(declared);
        legacySelected.ShouldBeTrue();
        statusMessageId.ShouldBe("legacy-status-1");
        completed!.Result.ShouldBeNull();
        httpContext.Items.ContainsKey(key).ShouldBeFalse();
    }

    [Fact]
    public async Task GeneratedCommandPublishesNoMessageIdVersionWhenValidationRefusesOrForQueries()
    {
        const string key = RouteCatalogGeneratedAction.MessageIdVersionKey;
        foreach ((Type contract, string kind, string tenant, bool expectedValid) in new[]
        {
            (typeof(IncrementCounter), "command", "Tenant-A", false),
            (typeof(GetCounterStatusQuery), "query", "tenant-a", true),
        })
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Items[key] = 1;
            (ActionExecutingContext executing, ActionContext actionContext) = CreateExecution(httpContext, contract, kind);
            bool validated = !expectedValid;
            bool presentAfterValidation = true;

            await new GeneratedRouteCatalogFilter(CreateAuthority()).OnActionExecutionAsync(executing, () =>
            {
                validated = RouteCatalogGeneratedAction.ValidateTenant(httpContext.Items, contract, kind, tenant);
                presentAfterValidation = httpContext.Items.ContainsKey(key);
                return Task.FromResult(new ActionExecutedContext(actionContext, [], executing.Controller));
            });

            validated.ShouldBe(expectedValid, kind);
            presentAfterValidation.ShouldBeFalse(kind);
            RouteCatalogGeneratedAction.TrySelectStatusMessageId(httpContext.Items, "legacy-status-1", out _).ShouldBeFalse(kind);
            httpContext.Items.ContainsKey(key).ShouldBeFalse(kind);
        }
    }

    private static IRouteCatalogAuthority CreateAuthority()
    {
        RouteCatalogEnvelope envelope = new RouteCatalogTestFixture().Envelope;
        var snapshot = new CommittedRouteCatalogSnapshot(RouteCatalogCodec.Encode(envelope), RouteCatalogCodec.ComputeRootDigest(envelope), 1);
        IRouteCatalogAuthority authority = Substitute.For<IRouteCatalogAuthority>();
        authority.AcquireAsync(Arg.Any<CancellationToken>()).Returns(_ => new RouteCatalogDispatchLease(snapshot, () => Task.CompletedTask));
        return authority;
    }

    private static (ActionExecutingContext Executing, ActionContext ActionContext) CreateExecution(HttpContext httpContext, Type contract, string kind)
    {
        var action = new ActionDescriptor { EndpointMetadata = [new RouteCatalogEndpointAttribute(contract, kind)] };
        var actionContext = new ActionContext(httpContext, new RouteData(), action);
        actionContext.RouteData.Values["tenant"] = "tenant-a";
        return (new ActionExecutingContext(actionContext, [], new Dictionary<string, object?>(), new object()), actionContext);
    }
}
