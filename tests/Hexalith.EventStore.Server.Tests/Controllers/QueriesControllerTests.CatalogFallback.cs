using System.Text.Json;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Queries;
using Hexalith.EventStore.Sample.Counter;
using Hexalith.EventStore.Server.Pipeline.Queries;
using Hexalith.EventStore.Server.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;
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
    public async Task CatalogCompletenessCannotCertifyGatewayActorFallback(string change)
    {
        var declaration = CounterCatalogDeclarations.Query;
        string queryType = change == "query-type" ? "uncataloged-query" : declaration.MessageType!;
        string? projectionType = change == "query-type" ? null : change == "projection-type" ? "uncataloged-projection" : declaration.ProjectionType;
        string actorType = change == "actor-type" ? "UncatalogedActor" : declaration.Execution!.ActorType!;
        // The gateway defaults an omitted projection to Domain; it still forwards unknown QueryType.
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
}
