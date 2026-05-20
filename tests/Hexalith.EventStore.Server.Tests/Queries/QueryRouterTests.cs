
using System.Text.Json;

using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Server.Pipeline.Queries;
using Hexalith.EventStore.Server.Queries;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Queries;

public class QueryRouterTests {
    private static SubmitQuery CreateTestQuery(
        string queryType = "GetOrderStatus",
        string domain = "orders",
        string aggregateId = "order-1",
        string? entityId = null,
        byte[]? payload = null,
        string? projectionType = null,
        string? projectionActorType = null,
        string tenant = "test-tenant") =>
        new(
            Tenant: tenant,
            Domain: domain,
            AggregateId: aggregateId,
            QueryType: queryType,
            Payload: payload ?? [],
            CorrelationId: "corr-1",
            UserId: "user-1",
            EntityId: entityId,
            ProjectionType: projectionType,
            ProjectionActorType: projectionActorType);

    private static (IProjectionActorInvoker invoker, QueryRouter router) CreateRouterWithInvoker(QueryResult? result = null) {
        IProjectionActorInvoker invoker = Substitute.For<IProjectionActorInvoker>();
        if (result is not null) {
            _ = invoker.InvokeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<QueryEnvelope>(), Arg.Any<CancellationToken>())
                .Returns(result);
        }

        var router = new QueryRouter(invoker, NullLogger<QueryRouter>.Instance);
        return (invoker, router);
    }

    [Fact]
    public async Task RouteQueryAsync_SuccessfulQuery_ReturnsResultWithPayload() {
        JsonElement resultPayload = JsonDocument.Parse("{\"status\":\"shipped\"}").RootElement;
        (IProjectionActorInvoker _, QueryRouter router) = CreateRouterWithInvoker(QueryResult.FromPayload(resultPayload));

        QueryRouterResult result = await router.RouteQueryAsync(CreateTestQuery());

        result.Success.ShouldBeTrue();
        result.NotFound.ShouldBeFalse();
        _ = result.Payload.ShouldNotBeNull();
        result.Payload!.Value.GetProperty("status").GetString().ShouldBe("shipped");
    }

    [Fact]
    public async Task RouteQueryAsync_RoutesToCorrectActor() {
        JsonElement resultPayload = JsonDocument.Parse("{}").RootElement;
        (IProjectionActorInvoker invoker, QueryRouter router) = CreateRouterWithInvoker(QueryResult.FromPayload(resultPayload));

        _ = await router.RouteQueryAsync(CreateTestQuery());

        _ = await invoker.Received(1).InvokeAsync(
            "GetOrderStatus:test-tenant",
            QueryRouter.ProjectionActorTypeName,
            Arg.Any<QueryEnvelope>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RouteQueryAsync_ActorMethodInvocationException_ReturnsNotFound() {
        IProjectionActorInvoker invoker = Substitute.For<IProjectionActorInvoker>();
        _ = invoker.InvokeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<QueryEnvelope>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ActorMethodInvocationException("actor type not registered", new InvalidOperationException(), false));

        var router = new QueryRouter(invoker, NullLogger<QueryRouter>.Instance);

        QueryRouterResult result = await router.RouteQueryAsync(CreateTestQuery());

        result.Success.ShouldBeFalse();
        result.NotFound.ShouldBeTrue();
        result.Payload.ShouldBeNull();
    }

    [Fact]
    public async Task RouteQueryAsync_GenericExceptionWithActorNotFoundPattern_ReturnsNotFound() {
        IProjectionActorInvoker invoker = Substitute.For<IProjectionActorInvoker>();
        _ = invoker.InvokeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<QueryEnvelope>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("did not find address for actor 'ProjectionActor/GetOrderStatus:test-tenant'"));

        var router = new QueryRouter(invoker, NullLogger<QueryRouter>.Instance);

        QueryRouterResult result = await router.RouteQueryAsync(CreateTestQuery());

        result.Success.ShouldBeFalse();
        result.NotFound.ShouldBeTrue();
        result.Payload.ShouldBeNull();
    }

    [Fact]
    public async Task RouteQueryAsync_ActorMethodInvocationExceptionWithoutNotFoundPattern_ReturnsActorExceptionCategory() {
        IProjectionActorInvoker invoker = Substitute.For<IProjectionActorInvoker>();
        _ = invoker.InvokeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<QueryEnvelope>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ActorMethodInvocationException("projection query failed", new InvalidOperationException("boom"), false));

        var router = new QueryRouter(invoker, NullLogger<QueryRouter>.Instance);

        QueryRouterResult result = await router.RouteQueryAsync(CreateTestQuery());

        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldBe(QueryAdapterFailureReason.ActorException);
    }

    [Fact]
    public async Task RouteQueryAsync_ProjectionActorReturnsFailure_ReturnsFailedResult() {
        (IProjectionActorInvoker _, QueryRouter router) = CreateRouterWithInvoker(QueryResult.Failure("projection unavailable"));

        QueryRouterResult result = await router.RouteQueryAsync(CreateTestQuery());

        result.Success.ShouldBeFalse();
        result.NotFound.ShouldBeFalse();
        result.Payload.ShouldBeNull();
        result.ErrorMessage.ShouldBe("projection unavailable");
    }

    [Fact]
    public async Task RouteQueryAsync_GenericNotRegisteredMessage_ReturnsActorExceptionCategory() {
        IProjectionActorInvoker invoker = Substitute.For<IProjectionActorInvoker>();
        _ = invoker.InvokeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<QueryEnvelope>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ActorMethodInvocationException("serializer dependency not registered", new InvalidOperationException("boom"), false));

        var router = new QueryRouter(invoker, NullLogger<QueryRouter>.Instance);

        QueryRouterResult result = await router.RouteQueryAsync(CreateTestQuery());

        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldBe(QueryAdapterFailureReason.ActorException);
    }

    [Fact]
    public async Task RouteQueryAsync_WithEntityId_RoutesToTier1ActorId() {
        JsonElement resultPayload = JsonDocument.Parse("{}").RootElement;
        (IProjectionActorInvoker invoker, QueryRouter router) = CreateRouterWithInvoker(QueryResult.FromPayload(resultPayload));

        _ = await router.RouteQueryAsync(CreateTestQuery(entityId: "order-123"));

        _ = await invoker.Received(1).InvokeAsync(
            "GetOrderStatus:test-tenant:order-123",
            QueryRouter.ProjectionActorTypeName,
            Arg.Any<QueryEnvelope>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RouteQueryAsync_WithNonEmptyPayloadAndNoEntityId_RoutesToTier2ActorId() {
        JsonElement resultPayload = JsonDocument.Parse("{}").RootElement;
        (IProjectionActorInvoker invoker, QueryRouter router) = CreateRouterWithInvoker(QueryResult.FromPayload(resultPayload));
        byte[] payload = [0x01, 0x02, 0x03];

        _ = await router.RouteQueryAsync(CreateTestQuery(payload: payload));

        _ = await invoker.Received(1).InvokeAsync(
            Arg.Is<string>(id => id.StartsWith("GetOrderStatus:test-tenant:", StringComparison.Ordinal) && id.Split(':')[2].Length == 11),
            QueryRouter.ProjectionActorTypeName,
            Arg.Any<QueryEnvelope>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RouteQueryAsync_WithEmptyPayloadAndNoEntityId_RoutesToTier3ActorId() {
        JsonElement resultPayload = JsonDocument.Parse("{}").RootElement;
        (IProjectionActorInvoker invoker, QueryRouter router) = CreateRouterWithInvoker(QueryResult.FromPayload(resultPayload));

        _ = await router.RouteQueryAsync(CreateTestQuery());

        _ = await invoker.Received(1).InvokeAsync(
            "GetOrderStatus:test-tenant",
            QueryRouter.ProjectionActorTypeName,
            Arg.Any<QueryEnvelope>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RouteQueryAsync_ProjectionType_PassesThroughFromQueryResult() {
        JsonElement resultPayload = JsonDocument.Parse("{\"status\":\"shipped\"}").RootElement;
        (IProjectionActorInvoker _, QueryRouter router) = CreateRouterWithInvoker(QueryResult.FromPayload(resultPayload, "order-list"));

        QueryRouterResult result = await router.RouteQueryAsync(CreateTestQuery());

        result.Success.ShouldBeTrue();
        result.ProjectionType.ShouldBe("order-list");
    }

    [Fact]
    public async Task RouteQueryAsync_NullProjectionType_PassesThroughNull() {
        JsonElement resultPayload = JsonDocument.Parse("{}").RootElement;
        (IProjectionActorInvoker _, QueryRouter router) = CreateRouterWithInvoker(QueryResult.FromPayload(resultPayload));

        QueryRouterResult result = await router.RouteQueryAsync(CreateTestQuery());

        result.ProjectionType.ShouldBeNull();
    }

    [Fact]
    public async Task RouteQueryAsync_ConstructsCorrectQueryEnvelope() {
        JsonElement resultPayload = JsonDocument.Parse("{}").RootElement;
        (IProjectionActorInvoker invoker, QueryRouter router) = CreateRouterWithInvoker(QueryResult.FromPayload(resultPayload));

        _ = await router.RouteQueryAsync(CreateTestQuery());

        _ = await invoker.Received(1).InvokeAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Is<QueryEnvelope>(e =>
                e.TenantId == "test-tenant" &&
                e.Domain == "orders" &&
                e.AggregateId == "order-1" &&
                e.QueryType == "GetOrderStatus" &&
                e.CorrelationId == "corr-1" &&
                e.UserId == "user-1" &&
                e.EntityId == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RouteQueryAsync_GetParty_UsesEntityScopedPublicAdapterRoute() {
        JsonElement resultPayload = JsonDocument.Parse("{\"id\":\"party-42\"}").RootElement;
        (IProjectionActorInvoker invoker, QueryRouter router) = CreateRouterWithInvoker(QueryResult.FromPayload(resultPayload, "party"));

        _ = await router.RouteQueryAsync(CreateTestQuery(
            queryType: "get-party",
            domain: "parties",
            aggregateId: "party",
            entityId: "party-42",
            projectionType: "party"));

        _ = await invoker.Received(1).InvokeAsync(
            "party:test-tenant:party-42",
            QueryRouter.ProjectionActorTypeName,
            Arg.Is<QueryEnvelope>(e =>
                e.QueryType == "get-party" &&
                e.Domain == "parties" &&
                e.EntityId == "party-42"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RouteQueryAsync_ListParties_UsesTenantWideRouteAndActorTypeOverride() {
        JsonElement resultPayload = JsonDocument.Parse("{\"items\":[]}").RootElement;
        (IProjectionActorInvoker invoker, QueryRouter router) = CreateRouterWithInvoker(QueryResult.FromPayload(resultPayload, "party-list"));

        _ = await router.RouteQueryAsync(CreateTestQuery(
            queryType: "list-parties",
            domain: "parties",
            aggregateId: "party",
            projectionType: "party-list",
            projectionActorType: "PartiesProjectionActor"));

        _ = await invoker.Received(1).InvokeAsync(
            "party-list:test-tenant",
            "PartiesProjectionActor",
            Arg.Any<QueryEnvelope>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RouteQueryAsync_SearchParties_UsesPayloadChecksumRouteWithoutPayloadData() {
        JsonElement resultPayload = JsonDocument.Parse("{\"items\":[]}").RootElement;
        (IProjectionActorInvoker invoker, QueryRouter router) = CreateRouterWithInvoker(QueryResult.FromPayload(resultPayload, "party-search"));
        byte[] payload = [1, 2, 3];

        _ = await router.RouteQueryAsync(CreateTestQuery(
            queryType: "search-parties",
            domain: "parties",
            aggregateId: "party",
            payload: payload,
            projectionType: "party-search"));

        _ = await invoker.Received(1).InvokeAsync(
            "party-search:test-tenant:A5BYxvLAy0k",
            QueryRouter.ProjectionActorTypeName,
            Arg.Is<QueryEnvelope>(e =>
                e.QueryType == "search-parties" &&
                e.Payload.SequenceEqual(payload)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RouteQueryAsync_SuccessWithoutPayload_ReturnsMissingPayloadCategory() {
        (IProjectionActorInvoker _, QueryRouter router) = CreateRouterWithInvoker(new QueryResult(true));

        QueryRouterResult result = await router.RouteQueryAsync(CreateTestQuery());

        result.Success.ShouldBeFalse();
        result.NotFound.ShouldBeFalse();
        result.Payload.ShouldBeNull();
        result.ErrorMessage.ShouldBe(QueryAdapterFailureReason.MissingPayload);
    }

    [Fact]
    public async Task RouteQueryAsync_InvalidPayloadBytes_ReturnsSerializationFailureCategory() {
        (IProjectionActorInvoker _, QueryRouter router) = CreateRouterWithInvoker(new QueryResult(true, [0xFF]));

        QueryRouterResult result = await router.RouteQueryAsync(CreateTestQuery());

        result.Success.ShouldBeFalse();
        result.NotFound.ShouldBeFalse();
        result.Payload.ShouldBeNull();
        result.ErrorMessage.ShouldBe(QueryAdapterFailureReason.SerializationFailure);
    }

    [Fact]
    public async Task RouteQueryAsync_NullActorResult_ReturnsActorResponseMismatchCategory() {
        IProjectionActorInvoker invoker = Substitute.For<IProjectionActorInvoker>();
        _ = invoker.InvokeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<QueryEnvelope>(), Arg.Any<CancellationToken>())
            .Returns((QueryResult)null!);

        var router = new QueryRouter(invoker, NullLogger<QueryRouter>.Instance);

        QueryRouterResult result = await router.RouteQueryAsync(CreateTestQuery());

        result.Success.ShouldBeFalse();
        result.NotFound.ShouldBeFalse();
        result.Payload.ShouldBeNull();
        result.ErrorMessage.ShouldBe(QueryAdapterFailureReason.ActorResponseMismatch);
    }

    [Fact]
    public async Task RouteQueryAsync_ActorMethodInvocationException_ReturnsActorExceptionCategory() {
        IProjectionActorInvoker invoker = Substitute.For<IProjectionActorInvoker>();
        _ = invoker.InvokeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<QueryEnvelope>(), Arg.Any<CancellationToken>())
            .Throws(new ActorMethodInvocationException("actor invocation failed", new InvalidOperationException("inner"), false));

        var router = new QueryRouter(invoker, NullLogger<QueryRouter>.Instance);

        QueryRouterResult result = await router.RouteQueryAsync(CreateTestQuery());

        result.Success.ShouldBeFalse();
        result.NotFound.ShouldBeFalse();
        result.Payload.ShouldBeNull();
        result.ErrorMessage.ShouldBe(QueryAdapterFailureReason.ActorException);
    }

    [Fact]
    public async Task RouteQueryAsync_GenericException_ReturnsActorExceptionCategory() {
        IProjectionActorInvoker invoker = Substitute.For<IProjectionActorInvoker>();
        _ = invoker.InvokeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<QueryEnvelope>(), Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("unexpected failure"));

        var router = new QueryRouter(invoker, NullLogger<QueryRouter>.Instance);

        QueryRouterResult result = await router.RouteQueryAsync(CreateTestQuery());

        result.Success.ShouldBeFalse();
        result.NotFound.ShouldBeFalse();
        result.Payload.ShouldBeNull();
        result.ErrorMessage.ShouldBe(QueryAdapterFailureReason.ActorException);
    }

    [Fact]
    public async Task RouteQueryAsync_NullReferenceFromActorInvocation_ReturnsActorExceptionCategory() {
        // R22A1: the original NRE-on-cast bug surfaced as NullReferenceException from
        // Dapr.Actors.Client.ActorProxy.InvokeMethodAsync<TRequest,TResponse>. The router
        // must classify this as ActorException (fail-closed) and not leak the NRE.
        IProjectionActorInvoker invoker = Substitute.For<IProjectionActorInvoker>();
        _ = invoker.InvokeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<QueryEnvelope>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new NullReferenceException("Object reference not set to an instance of an object."));

        var router = new QueryRouter(invoker, NullLogger<QueryRouter>.Instance);

        QueryRouterResult result = await router.RouteQueryAsync(CreateTestQuery());

        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldBe(QueryAdapterFailureReason.ActorException);
    }

    [Fact]
    public async Task RouteQueryAsync_CancellationRequested_ThrowsOperationCanceledException() {
        IProjectionActorInvoker invoker = Substitute.For<IProjectionActorInvoker>();
        _ = invoker.InvokeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<QueryEnvelope>(), Arg.Any<CancellationToken>())
            .Throws(new OperationCanceledException());

        var router = new QueryRouter(invoker, NullLogger<QueryRouter>.Instance);

        _ = await Should.ThrowAsync<OperationCanceledException>(
            () => router.RouteQueryAsync(CreateTestQuery()));
    }

    [Fact]
    public async Task RouteQueryAsync_PreCancelledToken_ThrowsBeforeInvokerCalled() {
        IProjectionActorInvoker invoker = Substitute.For<IProjectionActorInvoker>();
        var router = new QueryRouter(invoker, NullLogger<QueryRouter>.Instance);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(
            () => router.RouteQueryAsync(CreateTestQuery(), cts.Token));

        _ = await invoker.DidNotReceive().InvokeAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<QueryEnvelope>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RouteQueryAsync_OperationCanceledException_IsNotConvertedToAdapterFailure() {
        // Story AC #3: when the weak actor invocation observes an OperationCanceledException,
        // the same cancellation category must propagate — it must NOT be flattened into a
        // QueryAdapterFailureReason.ActorException result.
        //
        // ROOT CAUSE (investigated 2026-05-19): The .NET async state machine catches any
        // OperationCanceledException thrown inside an async method and transitions the returned
        // Task to TaskStatus.Canceled. When that canceled Task is later awaited, the runtime
        // surfaces a fresh System.Threading.Tasks.TaskCanceledException with the default
        // "A task was canceled." message — the original OCE instance, its concrete subclass,
        // its custom Message, and its inner exception are all lost. This is .NET behavior, not
        // NSubstitute or the DAPR actor SDK: a hand-rolled test double that throws a sentinel
        // OCE subclass inline produces the same fresh TaskCanceledException after the await.
        // Therefore message-text assertions on the surfaced exception cannot succeed for any
        // OCE that flows through async/await; the meaningful assertion is type identity plus
        // the absence of conversion to a QueryAdapterFailureReason (AC9). The structured-log
        // assertion below (no QueryExecutionFailed log emitted) hardens this further: had the
        // production code converted OCE to ActorException, that log would fire with a
        // QueryAdapterFailureReason and the test would fail.
        IProjectionActorInvoker invoker = Substitute.For<IProjectionActorInvoker>();
        _ = invoker.InvokeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<QueryEnvelope>(), Arg.Any<CancellationToken>())
            .Throws(new OperationCanceledException("abandoned request"));

        ILogger<QueryRouter> logger = Substitute.For<ILogger<QueryRouter>>();
        var router = new QueryRouter(invoker, logger);

        OperationCanceledException exception = await Should.ThrowAsync<OperationCanceledException>(
            () => router.RouteQueryAsync(CreateTestQuery()));

        // The exception is OCE-derived (catches conversion to an adapter failure type).
        _ = exception.ShouldBeAssignableTo<OperationCanceledException>();

        // It is not converted to a Hexalith adapter failure type. The surfaced OCE is exactly
        // TaskCanceledException (runtime-minted on Task.Canceled awaits) or
        // OperationCanceledException — never a wrapped/derived adapter exception type.
        Type exceptionType = exception.GetType();
        bool isExpectedCancellationType = exceptionType == typeof(OperationCanceledException)
            || exceptionType == typeof(TaskCanceledException);
        isExpectedCancellationType.ShouldBeTrue(
            $"Expected OperationCanceledException or TaskCanceledException but got {exceptionType.FullName}");

        // AC9: cancellation must not be logged as a QueryAdapterFailureReason. If the
        // production code's catch (OperationCanceledException) { throw; } block were removed
        // or reordered after the generic catch, QueryExecutionFailed (EventId 1204) or
        // ActorInvocationFailed (EventId 1202) would fire with an adapter failure reason.
        // Assert that no such log was emitted.
        logger.DidNotReceive().Log(
            Arg.Any<LogLevel>(),
            Arg.Is<EventId>(id => id.Id == 1204 || id.Id == 1202),
            Arg.Any<object?>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object?, Exception?, string>>()!);
    }

    [Fact]
    public async Task RouteQueryAsync_OperationCanceledExceptionWithNotFoundMessage_IsNotConvertedToNotFound() {
        IProjectionActorInvoker invoker = Substitute.For<IProjectionActorInvoker>();
        _ = invoker.InvokeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<QueryEnvelope>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException("did not find address for actor before cancellation completed"));

        var router = new QueryRouter(invoker, NullLogger<QueryRouter>.Instance);

        _ = await Should.ThrowAsync<OperationCanceledException>(
            () => router.RouteQueryAsync(CreateTestQuery()));
    }

    [Fact]
    public async Task RouteQueryAsync_PassesCallerCancellationTokenIntoInvoker() {
        // Story AC #3: the same request-scope CancellationToken must reach the weak
        // ActorProxy invocation path. The invoker call captures the token so we can
        // assert identity (not just default(CancellationToken)).
        CancellationToken capturedToken = default;
        IProjectionActorInvoker invoker = Substitute.For<IProjectionActorInvoker>();
        _ = invoker.InvokeAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<QueryEnvelope>(),
                Arg.Do<CancellationToken>(ct => capturedToken = ct))
            .Returns(QueryResult.FromPayload(JsonDocument.Parse("{}").RootElement));

        var router = new QueryRouter(invoker, NullLogger<QueryRouter>.Instance);
        using var cts = new CancellationTokenSource();

        _ = await router.RouteQueryAsync(CreateTestQuery(), cts.Token);

        capturedToken.ShouldBe(cts.Token);
    }

    [Fact]
    public async Task RouteQueryAsync_ListTenants_UsesTenantsProjectionActorAtSystemIndex() {
        // Story AC #2: list-tenants regression pin. The Admin UI Tenants page request shape
        // (Tenant=system, ProjectionType=tenants, EntityId=index, ProjectionActorType=TenantsProjectionActor)
        // must derive ActorId="tenants:system:index" and route to actor type "TenantsProjectionActor",
        // and preserve every QueryEnvelope field the public contract carries.
        JsonElement resultPayload = JsonDocument.Parse("{\"items\":[]}").RootElement;
        byte[] requestPayload = JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, object?> {
            ["cursor"] = null,
            ["pageSize"] = 100,
        });
        IProjectionActorInvoker invoker = Substitute.For<IProjectionActorInvoker>();
        _ = invoker.InvokeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<QueryEnvelope>(), Arg.Any<CancellationToken>())
            .Returns(QueryResult.FromPayload(resultPayload, "tenants"));

        var router = new QueryRouter(invoker, NullLogger<QueryRouter>.Instance);

        QueryRouterResult result = await router.RouteQueryAsync(CreateTestQuery(
            tenant: "system",
            queryType: "list-tenants",
            domain: "tenants",
            aggregateId: "index",
            entityId: "index",
            payload: requestPayload,
            projectionType: "tenants",
            projectionActorType: "TenantsProjectionActor"));

        _ = await invoker.Received(1).InvokeAsync(
            "tenants:system:index",
            "TenantsProjectionActor",
            Arg.Is<QueryEnvelope>(e =>
                e.TenantId == "system" &&
                e.Domain == "tenants" &&
                e.AggregateId == "index" &&
                e.QueryType == "list-tenants" &&
                e.CorrelationId == "corr-1" &&
                e.UserId == "user-1" &&
                e.EntityId == "index" &&
                e.Payload.SequenceEqual(requestPayload)),
            Arg.Any<CancellationToken>());
        result.Success.ShouldBeTrue();
        result.ProjectionType.ShouldBe("tenants");
        _ = result.Payload.ShouldNotBeNull();
        result.Payload!.Value.GetProperty("items").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task QueryRouter_ConstructedFromIActorProxyFactory_DoesNotCreateTypedDispatchProxyOnRoute() {
        // Regression-sensitive guard for the original NRE bug: the public DI constructor
        // accepts IActorProxyFactory, but the production path must NEVER call
        // CreateActorProxy<IProjectionActor>(...) on it (that returned the typed dispatch
        // proxy whose weak-state was not initialized, producing the runtime NRE).
        // This test would fail against the old implementation, where the first query called
        // CreateActorProxy<IProjectionActor>(...) before trying the weak invocation path.
        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        var router = new QueryRouter(factory, NullLogger<QueryRouter>.Instance);

        QueryRouterResult result = await router.RouteQueryAsync(CreateTestQuery());

        result.Success.ShouldBeFalse();
        _ = factory.Received(1).Create(
            Arg.Is<ActorId>(id => id.ToString() == "GetOrderStatus:test-tenant"),
            QueryRouter.ProjectionActorTypeName,
            Arg.Any<ActorProxyOptions?>());
        _ = factory.DidNotReceive().CreateActorProxy<IProjectionActor>(
            Arg.Any<ActorId>(),
            Arg.Any<string>());
        _ = factory.DidNotReceive().CreateActorProxy<IProjectionActor>(
            Arg.Any<ActorId>(),
            Arg.Any<string>(),
            Arg.Any<ActorProxyOptions?>());
    }
}
