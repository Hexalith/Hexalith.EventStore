using System.Text.Json;

using Dapr.Client;

using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Queries;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Pipeline.Queries;
using Hexalith.EventStore.Server.Queries;

using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Shouldly;

namespace Hexalith.EventStore.QueryRouting.Tests;

/// <summary>
/// Unit tests for the capability-declared routing decision in <see cref="HandlerAwareQueryRouter"/>.
/// The DAPR registry/invoker adapters are faked; their end-to-end behavior is covered by integration tests.
/// </summary>
public sealed class HandlerAwareQueryRouterTests {
    private static SubmitQuery Query(string domain, string queryType, QueryPagingOptions? paging = null)
        => new("test-tenant", domain, $"{domain}-1", queryType, [], "corr-1", "test-user", Paging: paging);

    [Fact]
    public async Task RouteQueryAsync_HandlerBased_InvokesDomainQueryEndpointAndDoesNotDelegate() {
        IDomainQueryHandlerRegistry registry = Substitute.For<IDomainQueryHandlerRegistry>();
        _ = registry.SupportsQueryAsync("widget", "get-widget", Arg.Any<CancellationToken>()).Returns(true);
        IQueryRouter inner = Substitute.For<IQueryRouter>();
        IDomainQueryInvoker invoker = Substitute.For<IDomainQueryInvoker>();
        JsonElement payload = JsonSerializer.SerializeToElement(new { value = 42 });
        var metadata = new QueryResponseMetadata(IsStale: false, ProjectionVersion: "widget-v1");
        _ = invoker.InvokeAsync(Arg.Any<QueryEnvelope>(), Arg.Any<CancellationToken>())
            .Returns(QueryResult.FromPayload(payload, projectionType: "widget", metadata));

        var router = new HandlerAwareQueryRouter(inner, registry, invoker, NullLogger<HandlerAwareQueryRouter>.Instance);

        QueryRouterResult result = await router.RouteQueryAsync(Query("widget", "get-widget"));

        result.Success.ShouldBeTrue();
        result.ProjectionType.ShouldBe("widget");
        result.Metadata.ShouldBe(metadata);
        await invoker.Received(1).InvokeAsync(Arg.Any<QueryEnvelope>(), Arg.Any<CancellationToken>());
        await inner.DidNotReceive().RouteQueryAsync(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RouteQueryAsync_HandlerBased_ForwardsPagingToDomainQueryEndpoint() {
        IDomainQueryHandlerRegistry registry = Substitute.For<IDomainQueryHandlerRegistry>();
        _ = registry.SupportsQueryAsync("widget", "get-widget", Arg.Any<CancellationToken>()).Returns(true);
        IQueryRouter inner = Substitute.For<IQueryRouter>();
        IDomainQueryInvoker invoker = Substitute.For<IDomainQueryInvoker>();
        JsonElement payload = JsonSerializer.SerializeToElement(new { value = 42 });
        _ = invoker.InvokeAsync(Arg.Any<QueryEnvelope>(), Arg.Any<CancellationToken>())
            .Returns(QueryResult.FromPayload(payload, projectionType: "widget"));
        var router = new HandlerAwareQueryRouter(inner, registry, invoker, NullLogger<HandlerAwareQueryRouter>.Instance);

        _ = await router.RouteQueryAsync(Query("widget", "get-widget", new QueryPagingOptions(PageSize: 25, Cursor: "opaque-cursor")));

        await invoker.Received(1).InvokeAsync(
            Arg.Is<QueryEnvelope>(e =>
                e.Paging != null &&
                e.Paging.PageSize == 25 &&
                e.Paging.Cursor == "opaque-cursor"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RouteQueryAsync_NotHandlerBased_DelegatesToInnerRouter() {
        IDomainQueryHandlerRegistry registry = Substitute.For<IDomainQueryHandlerRegistry>();
        _ = registry.SupportsQueryAsync("counter", "get-counter", Arg.Any<CancellationToken>()).Returns(false);
        IQueryRouter inner = Substitute.For<IQueryRouter>();
        var metadata = new QueryResponseMetadata(IsStale: true, ProjectionVersion: "counter-v3");
        var expected = new QueryRouterResult(
            Success: true,
            Payload: JsonSerializer.SerializeToElement(new { count = 1 }),
            NotFound: false,
            ProjectionType: "counter",
            Metadata: metadata);
        SubmitQuery query = Query("counter", "get-counter");
        _ = inner.RouteQueryAsync(query, Arg.Any<CancellationToken>()).Returns(expected);
        IDomainQueryInvoker invoker = Substitute.For<IDomainQueryInvoker>();

        var router = new HandlerAwareQueryRouter(inner, registry, invoker, NullLogger<HandlerAwareQueryRouter>.Instance);

        QueryRouterResult result = await router.RouteQueryAsync(query);

        result.ShouldBe(expected);
        result.Metadata.ShouldBe(metadata);
        await inner.Received(1).RouteQueryAsync(query, Arg.Any<CancellationToken>());
        await invoker.DidNotReceive().InvokeAsync(Arg.Any<QueryEnvelope>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RouteQueryAsync_HandlerInvokerReturnsFailure_ReturnsUnsuccessfulResult() {
        IDomainQueryHandlerRegistry registry = Substitute.For<IDomainQueryHandlerRegistry>();
        _ = registry.SupportsQueryAsync("widget", "get-widget", Arg.Any<CancellationToken>()).Returns(true);
        IDomainQueryInvoker invoker = Substitute.For<IDomainQueryInvoker>();
        var metadata = new QueryResponseMetadata(IsDegraded: true, WarningCodes: [QueryWarningCodes.DegradedSearch]);
        _ = invoker.InvokeAsync(Arg.Any<QueryEnvelope>(), Arg.Any<CancellationToken>())
            .Returns(QueryResult.Failure("domain unavailable", metadata));

        var router = new HandlerAwareQueryRouter(Substitute.For<IQueryRouter>(), registry, invoker, NullLogger<HandlerAwareQueryRouter>.Instance);

        QueryRouterResult result = await router.RouteQueryAsync(Query("widget", "get-widget"));

        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldBe("domain unavailable");
        result.Metadata.ShouldBe(metadata);
    }

    [Fact]
    public async Task SupportsQueryAsync_ReadsStateStoreAndCachesQueryTypes() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        _ = daprClient.GetStateAsync<List<string>>(
                "statestore",
                "admin:query-types:widget",
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(["get-widget"]);
        var registry = new DaprDomainQueryHandlerRegistry(
            daprClient,
            Options.Create(new CommandStatusOptions { StateStoreName = "statestore" }),
            NullLogger<DaprDomainQueryHandlerRegistry>.Instance);

        bool first = await registry.SupportsQueryAsync("widget", "get-widget");
        bool second = await registry.SupportsQueryAsync("widget", "GET-WIDGET");

        first.ShouldBeTrue();
        second.ShouldBeTrue();
        _ = await daprClient.Received(1).GetStateAsync<List<string>>(
            "statestore",
            "admin:query-types:widget",
            cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SupportsQueryAsync_StateStoreReadFailure_FailsSafeToProjectionRouter() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        _ = daprClient.GetStateAsync<List<string>>(
                "statestore",
                "admin:query-types:widget",
                cancellationToken: Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("state unavailable"));
        var registry = new DaprDomainQueryHandlerRegistry(
            daprClient,
            Options.Create(new CommandStatusOptions { StateStoreName = "statestore" }),
            NullLogger<DaprDomainQueryHandlerRegistry>.Instance);

        bool supported = await registry.SupportsQueryAsync("widget", "get-widget");

        supported.ShouldBeFalse();
    }
}
