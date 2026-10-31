using Dapr.Actors.Client;
using Dapr.Actors.Runtime;

using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.Sample.Counter;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Queries;
using Hexalith.EventStore.Server.Routing;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

public sealed class RunScopedProjectionCatalogTests
{
    [Theory]
    [InlineData("valid")]
    [InlineData("missing-actor")]
    [InlineData("wrong-actor")]
    [InlineData("missing-invoker")]
    [InlineData("wrong-invoker")]
    [InlineData("foreign-actor")]
    [InlineData("wrong-operation")]
    public void GatewayClaimsOnlyItsActualCanonicalActorAndInvocationPath(string fault)
    {
        var services = new ServiceCollection();
        var options = new ActorRuntimeOptions();
        if (fault == "wrong-actor") options.Actors.RegisterActor<ETagActor>(QueryRouter.ProjectionActorTypeName);
        else if (fault != "missing-actor") options.Actors.RegisterActor<EventReplayProjectionActor>(QueryRouter.ProjectionActorTypeName);
        services.AddSingleton<IOptions<ActorRuntimeOptions>>(Options.Create(options));
        if (fault == "wrong-invoker") services.AddSingleton(Substitute.For<IProjectionActorInvoker>());
        else if (fault != "missing-invoker")
            services.AddSingleton<IProjectionActorInvoker>(new DefaultProjectionActorInvoker(Substitute.For<IActorProxyFactory>()));
        var query = CounterCatalogDeclarations.Query;
        if (fault == "foreign-actor") query = query with { Execution = query.Execution! with { ActorType = "OtherActor" } };
        if (fault == "wrong-operation") query = query with { Operation = "eventstore:domainservice-query" };
        using var provider = services.BuildServiceProvider();
        if (fault is "foreign-actor" or "wrong-operation")
        {
            Should.Throw<RouteCatalogValidationException>(() => RunScopedRouteCatalogGateway.ProjectionTargets(provider, "gateway-private", [query]));
            return;
        }
        if (fault != "valid")
        {
            Should.Throw<RouteCatalogUnavailableException>(() => RunScopedRouteCatalogGateway.ProjectionTargets(provider, "gateway-private", [query]));
            return;
        }
        var target = RunScopedRouteCatalogGateway.ProjectionTargets(provider, "gateway-private", [query]).ShouldHaveSingleItem();
        target.AppId.ShouldBe("gateway-private");
        target.Method.ShouldBe("QueryAsync");
        target.RouteId.ShouldBe(query.RouteId);
        target.DeclarationDigest.ShouldBe(RouteCatalogCodec.ComputeDeclarationDigest(query));
        target.ContractVersion.ShouldBe("v1");
        target.Operation.ShouldBe(query.Operation);
        target.IsRuntimeOverride.ShouldBeFalse();
    }

    [Fact]
    public void GatewayDoesNotClaimDomainServiceCommands()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();
        RunScopedRouteCatalogGateway.ProjectionTargets(provider, "gateway-private", CounterCatalogDeclarations.Routes
            .Where(declaration => declaration.Kind == "command").ToArray()).ShouldBeEmpty();
    }
}
