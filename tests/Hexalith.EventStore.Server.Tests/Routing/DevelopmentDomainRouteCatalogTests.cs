using Hexalith.EventStore.Client.Discovery;
using Hexalith.EventStore.Client.Handlers;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Sample.Counter;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

public sealed class DevelopmentDomainRouteCatalogTests
{
    [Theory]
    [InlineData("valid")]
    [InlineData("undeclared-command")]
    [InlineData("undeclared-query")]
    [InlineData("undeclared-projection")]
    [InlineData("wrong-verb")]
    [InlineData("wrong-policy")]
    [InlineData("anonymous")]
    [InlineData("wrong-operation")]
    public void ActualReceiversMustMatchTheCompleteDeclaredInventoryAndCanonicalPolicies(string fault)
    {
        var fixture = new RouteCatalogTestFixture();
        var declarations = fixture.Envelope.Declarations.Where(d => d.Kind == "command" && d.Domain == "counter").ToArray();
        var canonical = EventStoreDomainServiceRoutes.Operational.Single(route => route.Route == "/process");
        declarations = declarations.Select(d => d with { Execution = d.Execution! with { TargetKind = "domain-service" } }).ToArray();
        var processor = Substitute.For<IDomainProcessor, IDomainCommandPayloadInventory>();
        ((IDomainCommandPayloadInventory)processor).CommandPayloadTypes.Returns(declarations.Select(d => AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType(d.Execution!.HandlerType!)).First(type => type is not null)!).ToArray());
        var services = new ServiceCollection();
        services.AddSingleton(new DiscoveryResult([new(typeof(CounterAggregate), "counter", typeof(object), DomainKind.Aggregate)], []));
        services.AddKeyedSingleton<IDomainProcessor>("counter", processor);
        services.AddSingleton<IOptions<DomainProjectionIdentityOptions>>(Options.Create(new DomainProjectionIdentityOptions { AppId = "counter-owner", ServiceVersion = "v1" }));
        if (fault == "undeclared-command") declarations = declarations.Skip(1).ToArray();
        if (fault == "wrong-operation") declarations = declarations.Select(d => d with { Operation = "wrong-operation" }).ToArray();
        if (fault == "undeclared-query")
        {
            var handler = Substitute.For<IDomainQueryHandler>();
            handler.Domain.Returns("counter"); handler.QueryType.Returns("undeclared-query"); services.AddSingleton(handler);
        }
        if (fault == "undeclared-projection")
        {
            var handler = Substitute.For<IAsyncDomainProjectionHandler>();
            handler.Domain.Returns("counter"); handler.ProjectionType.Returns("undeclared-projection"); services.AddSingleton(handler);
        }
        var endpoint = new RouteEndpointBuilder(_ => Task.CompletedTask, RoutePatternFactory.Parse("/process"), 0);
        endpoint.Metadata.Add(new HttpMethodMetadata([fault == "wrong-verb" ? "GET" : "POST"]));
        endpoint.Metadata.Add(new AuthorizeAttribute { Policy = fault == "wrong-policy" ? "wrong-policy" : canonical.Policy });
        if (fault == "anonymous") endpoint.Metadata.Add(new AllowAnonymousAttribute());
        using var provider = services.BuildServiceProvider();
        if (fault == "valid")
        {
            var targets = DevelopmentDomainRouteCatalog.Targets(provider, [endpoint.Build()], declarations);
            targets.Count.ShouldBe(declarations.Length);
            targets.ShouldAllBe(target => target.AppId == "counter-owner" && target.Method == "process" && target.ContractVersion == "v1");
        }
        else Should.Throw<RouteCatalogUnavailableException>(() => DevelopmentDomainRouteCatalog.Targets(provider, [endpoint.Build()], declarations));
    }
}
