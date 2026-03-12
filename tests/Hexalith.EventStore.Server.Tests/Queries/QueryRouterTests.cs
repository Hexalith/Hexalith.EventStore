
using System.Text.Json;

using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Pipeline.Queries;
using Hexalith.EventStore.Server.Queries;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Queries;

public class QueryRouterTests {
    private static SubmitQuery CreateTestQuery() =>
        new(
            Tenant: "test-tenant",
            Domain: "orders",
            AggregateId: "order-1",
            QueryType: "GetOrderStatus",
            Payload: [],
            CorrelationId: "corr-1",
            UserId: "user-1");

    [Fact]
    public async Task RouteQueryAsync_SuccessfulQuery_ReturnsResultWithPayload() {
        // Arrange
        JsonElement resultPayload = JsonDocument.Parse("{\"status\":\"shipped\"}").RootElement;
        var queryResult = new QueryResult(true, resultPayload);

        IProjectionActor actor = Substitute.For<IProjectionActor>();
        _ = actor.QueryAsync(Arg.Any<QueryEnvelope>()).Returns(queryResult);

        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        _ = factory.CreateActorProxy<IProjectionActor>(Arg.Any<ActorId>(), Arg.Any<string>())
            .Returns(actor);

        var router = new QueryRouter(factory, NullLogger<QueryRouter>.Instance);

        // Act
        QueryRouterResult result = await router.RouteQueryAsync(CreateTestQuery());

        // Assert
        result.Success.ShouldBeTrue();
        result.NotFound.ShouldBeFalse();
        result.Payload.ShouldNotBeNull();
        result.Payload!.Value.GetProperty("status").GetString().ShouldBe("shipped");
    }

    [Fact]
    public async Task RouteQueryAsync_RoutesToCorrectActor() {
        // Arrange
        JsonElement resultPayload = JsonDocument.Parse("{}").RootElement;
        IProjectionActor actor = Substitute.For<IProjectionActor>();
        _ = actor.QueryAsync(Arg.Any<QueryEnvelope>()).Returns(new QueryResult(true, resultPayload));

        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        _ = factory.CreateActorProxy<IProjectionActor>(Arg.Any<ActorId>(), Arg.Any<string>())
            .Returns(actor);

        var router = new QueryRouter(factory, NullLogger<QueryRouter>.Instance);
        SubmitQuery query = CreateTestQuery();

        // Act
        _ = await router.RouteQueryAsync(query);

        // Assert — verify actor proxy created with correct identity-derived actor ID and type name
        factory.Received(1).CreateActorProxy<IProjectionActor>(
            Arg.Is<ActorId>(id => id.ToString() == "test-tenant:orders:order-1"),
            QueryRouter.ProjectionActorTypeName);
    }

    [Fact]
    public async Task RouteQueryAsync_ActorMethodInvocationException_ReturnsNotFound() {
        // Arrange
        IProjectionActor actor = Substitute.For<IProjectionActor>();
        _ = actor.QueryAsync(Arg.Any<QueryEnvelope>())
            .ThrowsAsync(new ActorMethodInvocationException("actor type not registered", new InvalidOperationException(), false));

        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        _ = factory.CreateActorProxy<IProjectionActor>(Arg.Any<ActorId>(), Arg.Any<string>())
            .Returns(actor);

        var router = new QueryRouter(factory, NullLogger<QueryRouter>.Instance);

        // Act
        QueryRouterResult result = await router.RouteQueryAsync(CreateTestQuery());

        // Assert
        result.Success.ShouldBeFalse();
        result.NotFound.ShouldBeTrue();
        result.Payload.ShouldBeNull();
    }

    [Fact]
    public async Task RouteQueryAsync_GenericExceptionWithActorNotFoundPattern_ReturnsNotFound() {
        // Arrange
        IProjectionActor actor = Substitute.For<IProjectionActor>();
        _ = actor.QueryAsync(Arg.Any<QueryEnvelope>())
            .ThrowsAsync(new InvalidOperationException("did not find address for actor 'ProjectionActor/test-tenant:orders:order-1'"));

        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        _ = factory.CreateActorProxy<IProjectionActor>(Arg.Any<ActorId>(), Arg.Any<string>())
            .Returns(actor);

        var router = new QueryRouter(factory, NullLogger<QueryRouter>.Instance);

        // Act
        QueryRouterResult result = await router.RouteQueryAsync(CreateTestQuery());

        // Assert
        result.Success.ShouldBeFalse();
        result.NotFound.ShouldBeTrue();
        result.Payload.ShouldBeNull();
    }

    [Fact]
    public async Task RouteQueryAsync_ActorMethodInvocationExceptionWithoutNotFoundPattern_Propagates() {
        // Arrange
        IProjectionActor actor = Substitute.For<IProjectionActor>();
        _ = actor.QueryAsync(Arg.Any<QueryEnvelope>())
            .ThrowsAsync(new ActorMethodInvocationException("projection query failed", new InvalidOperationException("boom"), false));

        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        _ = factory.CreateActorProxy<IProjectionActor>(Arg.Any<ActorId>(), Arg.Any<string>())
            .Returns(actor);

        var router = new QueryRouter(factory, NullLogger<QueryRouter>.Instance);

        // Act & Assert
        _ = await Should.ThrowAsync<ActorMethodInvocationException>(
            () => router.RouteQueryAsync(CreateTestQuery()));
    }

    [Fact]
    public async Task RouteQueryAsync_ProjectionActorReturnsFailure_ReturnsFailedResult() {
        // Arrange
        JsonElement resultPayload = JsonDocument.Parse("{}").RootElement;
        IProjectionActor actor = Substitute.For<IProjectionActor>();
        _ = actor.QueryAsync(Arg.Any<QueryEnvelope>()).Returns(new QueryResult(false, resultPayload, "projection unavailable"));

        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        _ = factory.CreateActorProxy<IProjectionActor>(Arg.Any<ActorId>(), Arg.Any<string>())
            .Returns(actor);

        var router = new QueryRouter(factory, NullLogger<QueryRouter>.Instance);

        // Act
        QueryRouterResult result = await router.RouteQueryAsync(CreateTestQuery());

        // Assert
        result.Success.ShouldBeFalse();
        result.NotFound.ShouldBeFalse();
        result.Payload.ShouldBeNull();
        result.ErrorMessage.ShouldBe("projection unavailable");
    }

    [Fact]
    public async Task RouteQueryAsync_GenericNotRegisteredMessage_Propagates() {
        // Arrange
        IProjectionActor actor = Substitute.For<IProjectionActor>();
        _ = actor.QueryAsync(Arg.Any<QueryEnvelope>())
            .ThrowsAsync(new ActorMethodInvocationException("serializer dependency not registered", new InvalidOperationException("boom"), false));

        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        _ = factory.CreateActorProxy<IProjectionActor>(Arg.Any<ActorId>(), Arg.Any<string>())
            .Returns(actor);

        var router = new QueryRouter(factory, NullLogger<QueryRouter>.Instance);

        // Act & Assert
        _ = await Should.ThrowAsync<ActorMethodInvocationException>(
            () => router.RouteQueryAsync(CreateTestQuery()));
    }

    [Fact]
    public async Task RouteQueryAsync_ConstructsCorrectQueryEnvelope() {
        // Arrange
        JsonElement resultPayload = JsonDocument.Parse("{}").RootElement;
        IProjectionActor actor = Substitute.For<IProjectionActor>();
        _ = actor.QueryAsync(Arg.Any<QueryEnvelope>()).Returns(new QueryResult(true, resultPayload));

        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        _ = factory.CreateActorProxy<IProjectionActor>(Arg.Any<ActorId>(), Arg.Any<string>())
            .Returns(actor);

        var router = new QueryRouter(factory, NullLogger<QueryRouter>.Instance);
        SubmitQuery query = CreateTestQuery();

        // Act
        _ = await router.RouteQueryAsync(query);

        // Assert — verify QueryEnvelope fields match SubmitQuery fields
        await actor.Received(1).QueryAsync(Arg.Is<QueryEnvelope>(e =>
            e.TenantId == "test-tenant" &&
            e.Domain == "orders" &&
            e.AggregateId == "order-1" &&
            e.QueryType == "GetOrderStatus" &&
            e.CorrelationId == "corr-1" &&
            e.UserId == "user-1"));
    }
}
