using System.Text.Json;

using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Queries;
using Hexalith.EventStore.Server.Pipeline.Queries;
using Hexalith.EventStore.Server.Queries;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.QueryRouting.Tests;

/// <summary>
/// Unit tests for the capability-declared routing decision in <see cref="HandlerAwareQueryRouter"/>.
/// The DAPR registry/invoker adapters are faked; their end-to-end behavior is covered by integration tests.
/// </summary>
public sealed class HandlerAwareQueryRouterTests {
    private static SubmitQuery Query(string domain, string queryType)
        => new("test-tenant", domain, $"{domain}-1", queryType, [], "corr-1", "test-user");

    [Fact]
    public async Task RouteQueryAsync_HandlerBased_InvokesDomainQueryEndpointAndDoesNotDelegate() {
        IDomainQueryHandlerRegistry registry = Substitute.For<IDomainQueryHandlerRegistry>();
        _ = registry.SupportsQueryAsync("widget", "get-widget", Arg.Any<CancellationToken>()).Returns(true);
        IQueryRouter inner = Substitute.For<IQueryRouter>();
        IDomainQueryInvoker invoker = Substitute.For<IDomainQueryInvoker>();
        JsonElement payload = JsonSerializer.SerializeToElement(new { value = 42 });
        _ = invoker.InvokeAsync(Arg.Any<QueryEnvelope>(), Arg.Any<CancellationToken>())
            .Returns(QueryResult.FromPayload(payload, projectionType: "widget"));

        var router = new HandlerAwareQueryRouter(inner, registry, invoker, NullLogger<HandlerAwareQueryRouter>.Instance);

        QueryRouterResult result = await router.RouteQueryAsync(Query("widget", "get-widget"));

        result.Success.ShouldBeTrue();
        result.ProjectionType.ShouldBe("widget");
        await invoker.Received(1).InvokeAsync(Arg.Any<QueryEnvelope>(), Arg.Any<CancellationToken>());
        await inner.DidNotReceive().RouteQueryAsync(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RouteQueryAsync_NotHandlerBased_DelegatesToInnerRouter() {
        IDomainQueryHandlerRegistry registry = Substitute.For<IDomainQueryHandlerRegistry>();
        _ = registry.SupportsQueryAsync("counter", "get-counter", Arg.Any<CancellationToken>()).Returns(false);
        IQueryRouter inner = Substitute.For<IQueryRouter>();
        var expected = new QueryRouterResult(Success: true, Payload: JsonSerializer.SerializeToElement(new { count = 1 }), NotFound: false, ProjectionType: "counter");
        SubmitQuery query = Query("counter", "get-counter");
        _ = inner.RouteQueryAsync(query, Arg.Any<CancellationToken>()).Returns(expected);
        IDomainQueryInvoker invoker = Substitute.For<IDomainQueryInvoker>();

        var router = new HandlerAwareQueryRouter(inner, registry, invoker, NullLogger<HandlerAwareQueryRouter>.Instance);

        QueryRouterResult result = await router.RouteQueryAsync(query);

        result.ShouldBe(expected);
        await inner.Received(1).RouteQueryAsync(query, Arg.Any<CancellationToken>());
        await invoker.DidNotReceive().InvokeAsync(Arg.Any<QueryEnvelope>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RouteQueryAsync_HandlerInvokerReturnsFailure_ReturnsUnsuccessfulResult() {
        IDomainQueryHandlerRegistry registry = Substitute.For<IDomainQueryHandlerRegistry>();
        _ = registry.SupportsQueryAsync("widget", "get-widget", Arg.Any<CancellationToken>()).Returns(true);
        IDomainQueryInvoker invoker = Substitute.For<IDomainQueryInvoker>();
        _ = invoker.InvokeAsync(Arg.Any<QueryEnvelope>(), Arg.Any<CancellationToken>())
            .Returns(QueryResult.Failure("domain unavailable"));

        var router = new HandlerAwareQueryRouter(Substitute.For<IQueryRouter>(), registry, invoker, NullLogger<HandlerAwareQueryRouter>.Instance);

        QueryRouterResult result = await router.RouteQueryAsync(Query("widget", "get-widget"));

        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldBe("domain unavailable");
    }
}
