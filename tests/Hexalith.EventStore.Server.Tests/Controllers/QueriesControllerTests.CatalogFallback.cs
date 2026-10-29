using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Queries;
using Hexalith.EventStore.Sample.Counter;
using Hexalith.EventStore.Server.Pipeline.Queries;
using Hexalith.EventStore.Server.Queries;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Projections;
using Hexalith.EventStore.Server.Tests.Routing;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.EventStore.ServiceDefaults.Routing;
using Hexalith.Tenants.Contracts.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Controllers;

public partial class QueriesControllerTests
{
    [Theory]
    [InlineData("declared")]
    [InlineData("query-type")]
    [InlineData("projection-type")]
    [InlineData("actor-type")]
    public async Task CommittedCatalogGuardsActualControllerAndGatewayActorFallback(string change)
    {
        var declaration = CounterCatalogDeclarations.Query;
        string queryType = change == "query-type" ? "uncataloged-query" : declaration.MessageType!;
        string? projectionType = change == "query-type" ? null : change == "projection-type" ? "uncataloged-projection" : declaration.ProjectionType;
        string actorType = change == "actor-type" ? "UncatalogedActor" : declaration.Execution!.ActorType!;
        var catalog = new RouteCatalogTestFixture();
        await catalog.ActivateAsync(catalog.Candidate());
        string effectiveProjection = declaration.ProjectionType!;
        string target = effectiveProjection + ":test-tenant:counter-1";
        IProjectionActorInvoker actor = Substitute.For<IProjectionActorInvoker>();
        actor.InvokeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<QueryEnvelope>(), Arg.Any<CancellationToken>())
            .Returns(QueryResult.FromPayload(JsonSerializer.SerializeToElement(new { count = 1 }), effectiveProjection));
        IProjectionLifecycleGateway lifecycle = Substitute.For<IProjectionLifecycleGateway>();
        lifecycle.ReadSnapshotAsync(Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProjectionLifecycleSnapshot(ProjectionLifecyclePhase.Idle, 1));
        var inner = new QueryRouter(actor, NullLogger<QueryRouter>.Instance, lifecycle, catalogAuthority: catalog.Authorities[0]);
        IDomainQueryHandlerRegistry capabilities = Substitute.For<IDomainQueryHandlerRegistry>();
        IDomainQueryInvoker http = Substitute.For<IDomainQueryInvoker>();
        var router = new HandlerAwareQueryRouter(catalog.Authorities[0], inner, capabilities, http, NullLogger<HandlerAwareQueryRouter>.Instance);
        IMediator mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>()).Returns(async call =>
        {
            SubmitQuery submitted = call.Arg<SubmitQuery>();
            QueryRouterResult result = await router.RouteQueryAsync(submitted, call.Arg<CancellationToken>());
            result.Success.ShouldBeTrue();
            return new SubmitQueryResult(submitted.CorrelationId, result.Payload!.Value, result.ProjectionType, result.Metadata);
        });
        var controller = CreateController(mediator, catalogAuthority: catalog.Authorities[0]);
        var request = new SubmitQueryRequest("test-tenant", "counter", "counter-1", queryType, projectionType,
            JsonSerializer.SerializeToElement(new { }), "counter-1", actorType);
        (await new Hexalith.EventStore.Validation.SubmitQueryRequestValidator().ValidateAsync(request, TestContext.Current.CancellationToken)).IsValid.ShouldBeTrue();
        if (change == "declared")
        {
            (await controller.Submit(request, null, TestContext.Current.CancellationToken)).ShouldBeOfType<OkObjectResult>();
            await actor.Received(1).InvokeAsync(target, actorType,
                Arg.Is<QueryEnvelope>(envelope => envelope.QueryType == queryType && envelope.AggregateId == "counter-1"), Arg.Any<CancellationToken>());
        }
        else
        {
            await Should.ThrowAsync<RouteCatalogUnavailableException>(() => controller.Submit(request, null, TestContext.Current.CancellationToken));
            actor.ReceivedCalls().ShouldBeEmpty();
        }
        await http.DidNotReceiveWithAnyArgs().InvokeAsync(default!, default);
    }

    [Theory]
    [InlineData("declared")]
    [InlineData("query-type")]
    [InlineData("projection-type")]
    [InlineData("actor-type")]
    public async Task LegacyDirectConstructionRetainsHistoricalActorIdentityWithoutCatalog(string change)
    {
        var declaration = CounterCatalogDeclarations.Query;
        string queryType = change == "query-type" ? "uncataloged-query" : declaration.MessageType!;
        string? projectionType = change == "query-type" ? null : change == "projection-type" ? "uncataloged-projection" : declaration.ProjectionType;
        string actorType = change == "actor-type" ? "UncatalogedActor" : declaration.Execution!.ActorType!;
        // This explicit direct-construction seam has no catalog authority; preserve its historical actor identity.
        // Activated HTTP and controller/decorator controls enforce the runtime catalog.
        string effectiveProjection = projectionType ?? "counter";
        string target = effectiveProjection + ":test-tenant:counter-1";
        IProjectionActorInvoker actor = Substitute.For<IProjectionActorInvoker>();
        actor.InvokeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<QueryEnvelope>(), Arg.Any<CancellationToken>())
            .Returns(QueryResult.FromPayload(JsonSerializer.SerializeToElement(new { count = 1 }), effectiveProjection));
        var inner = new QueryRouter(actor, NullLogger<QueryRouter>.Instance);
        IDomainQueryHandlerRegistry capabilities = Substitute.For<IDomainQueryHandlerRegistry>();
        IDomainQueryInvoker http = Substitute.For<IDomainQueryInvoker>();
        var router = new HandlerAwareQueryRouter(inner, capabilities, http, NullLogger<HandlerAwareQueryRouter>.Instance);
        IMediator mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>()).Returns(async call =>
        {
            SubmitQuery submitted = call.Arg<SubmitQuery>();
            QueryRouterResult result = await router.RouteQueryAsync(submitted, call.Arg<CancellationToken>());
            result.Success.ShouldBeTrue();
            return new SubmitQueryResult(submitted.CorrelationId, result.Payload!.Value, result.ProjectionType, result.Metadata);
        });
        var controller = CreateController(mediator);
        var request = new SubmitQueryRequest("test-tenant", "counter", "counter-1", queryType, projectionType,
            JsonSerializer.SerializeToElement(new { }), "counter-1", actorType);
        (await new Hexalith.EventStore.Validation.SubmitQueryRequestValidator().ValidateAsync(request, TestContext.Current.CancellationToken)).IsValid.ShouldBeTrue();
        (await controller.Submit(request, null, TestContext.Current.CancellationToken)).ShouldBeOfType<OkObjectResult>();
        await actor.Received(1).InvokeAsync(target, actorType,
            Arg.Is<QueryEnvelope>(envelope => envelope.QueryType == queryType && envelope.AggregateId == "counter-1"), Arg.Any<CancellationToken>());
        await http.DidNotReceiveWithAnyArgs().InvokeAsync(default!, default);
    }

    [Theory]
    [InlineData("list-tenants")]
    [InlineData("get-user-tenants")]
    public async Task CatalogBackedSubmitReachesTheHandlerWhenProjectionIsOmitted(string queryType)
    {
        var catalog = new RouteCatalogTestFixture();
        await catalog.ActivateAsync(catalog.Candidate());
        IProjectionActorInvoker actor = Substitute.For<IProjectionActorInvoker>();
        var inner = new QueryRouter(actor, NullLogger<QueryRouter>.Instance, Substitute.For<IProjectionLifecycleGateway>(), catalogAuthority: catalog.Authorities[0]);
        IDomainQueryHandlerRegistry capabilities = Substitute.For<IDomainQueryHandlerRegistry>();
        IDomainQueryInvoker http = Substitute.For<IDomainQueryInvoker>();
        http.InvokeAsync(Arg.Any<QueryEnvelope>(), Arg.Any<CancellationToken>())
            .Returns(QueryResult.FromPayload(JsonSerializer.SerializeToElement(new { count = 0 })));
        var router = new HandlerAwareQueryRouter(catalog.Authorities[0], inner, capabilities, http, NullLogger<HandlerAwareQueryRouter>.Instance);
        IMediator mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>()).Returns(async call =>
        {
            SubmitQuery submitted = call.Arg<SubmitQuery>();
            submitted.ProjectionType.ShouldBeNull();
            QueryRouterResult result = await router.RouteQueryAsync(submitted, call.Arg<CancellationToken>());
            result.Success.ShouldBeTrue();
            return new SubmitQueryResult(submitted.CorrelationId, result.Payload!.Value, result.ProjectionType, result.Metadata);
        });
        var controller = CreateController(mediator, catalogAuthority: catalog.Authorities[0]);
        var request = new SubmitQueryRequest("test-tenant", "tenants", "tenant-1", queryType);
        (await new Hexalith.EventStore.Validation.SubmitQueryRequestValidator().ValidateAsync(request, TestContext.Current.CancellationToken)).IsValid.ShouldBeTrue();
        (await controller.Submit(request, null, TestContext.Current.CancellationToken)).ShouldBeOfType<OkObjectResult>();
        await http.Received(1).InvokeAsync(Arg.Is<QueryEnvelope>(envelope => envelope.Domain == "tenants" && envelope.QueryType == queryType), Arg.Any<CancellationToken>());
        actor.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task CatalogBackedOwnerAuthoritySubmitsResolveTenantOwnerAuthorityWithNullProjection()
    {
        using RSA key = RSA.Create(2048);
        var catalog = new RouteCatalogTestFixture();
        await catalog.ActivateAsync(catalog.Candidate());
        var query = new SubmitQueryRequest("tenant-a", "tenants", "tenant-a", ResolveTenantOwnerAuthority.QueryType, Payload: JsonSerializer.SerializeToElement(new { }));
        var request = new BoundOwnerQueryRequest(query, "actor-1");
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:BoundStreamRead:PublicKeys:projects"] = Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()),
            ["Authentication:BoundOwnerQuery:AllowedQueries:projects"] = ResolveTenantOwnerAuthority.QueryType,
        }).Build();
        IMediator mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(new SubmitQueryResult("correlation", JsonSerializer.SerializeToElement(new { }), null, null));
        var controller = CreateController(mediator, catalogAuthority: catalog.Authorities[0], configuration: configuration,
            principal: new ClaimsPrincipal(new ClaimsIdentity([
                new Claim(ClaimTypes.NameIdentifier, "workload:projects"),
                new Claim(EventStoreWorkloadAuthenticationDefaults.WorkloadClaimType, "projects")], "validated-workload")));
        controller.Request.Headers[BoundOwnerQueryProof.HeaderName] = BoundOwnerQueryProof.Issue(
            request, "projects", Convert.ToBase64String(key.ExportPkcs8PrivateKey()), DateTimeOffset.UtcNow);
        (await controller.SubmitBoundOwnerAuthorityAsync(request, TestContext.Current.CancellationToken)).ShouldBeOfType<OkObjectResult>();
        await mediator.Received(1).Send(Arg.Is<SubmitQuery>(submitted => submitted.QueryType == ResolveTenantOwnerAuthority.QueryType && submitted.ProjectionType == null), Arg.Any<CancellationToken>());
    }
}
