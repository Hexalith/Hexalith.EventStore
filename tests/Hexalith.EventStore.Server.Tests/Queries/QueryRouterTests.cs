
using System.Text.Json;

using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Server.Pipeline.Queries;
using Hexalith.EventStore.Server.Queries;

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
        string? projectionActorType = null) =>
        new(
            Tenant: "test-tenant",
            Domain: domain,
            AggregateId: aggregateId,
            QueryType: queryType,
            Payload: payload ?? [],
            CorrelationId: "corr-1",
            UserId: "user-1",
            EntityId: entityId,
            ProjectionType: projectionType,
            ProjectionActorType: projectionActorType);

    [Fact]
    public async Task RouteQueryAsync_SuccessfulQuery_ReturnsResultWithPayload() {
        // Arrange
        JsonElement resultPayload = JsonDocument.Parse("{\"status\":\"shipped\"}").RootElement;
        var queryResult = QueryResult.FromPayload(resultPayload);

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
        _ = result.Payload.ShouldNotBeNull();
        result.Payload!.Value.GetProperty("status").GetString().ShouldBe("shipped");
    }

    [Fact]
    public async Task RouteQueryAsync_RoutesToCorrectActor() {
        // Arrange
        JsonElement resultPayload = JsonDocument.Parse("{}").RootElement;
        IProjectionActor actor = Substitute.For<IProjectionActor>();
        _ = actor.QueryAsync(Arg.Any<QueryEnvelope>()).Returns(QueryResult.FromPayload(resultPayload));

        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        _ = factory.CreateActorProxy<IProjectionActor>(Arg.Any<ActorId>(), Arg.Any<string>())
            .Returns(actor);

        var router = new QueryRouter(factory, NullLogger<QueryRouter>.Instance);
        SubmitQuery query = CreateTestQuery();

        // Act
        _ = await router.RouteQueryAsync(query);

        // Assert — verify actor proxy created with correct 3-tier-derived actor ID (Tier 3: no EntityId, empty payload)
        _ = factory.Received(1).CreateActorProxy<IProjectionActor>(
            Arg.Is<ActorId>(id => id.ToString() == "GetOrderStatus:test-tenant"),
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
            .ThrowsAsync(new InvalidOperationException("did not find address for actor 'ProjectionActor/GetOrderStatus:test-tenant'"));

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
    public async Task RouteQueryAsync_ActorMethodInvocationExceptionWithoutNotFoundPattern_ReturnsActorExceptionCategory() {
        // Arrange
        IProjectionActor actor = Substitute.For<IProjectionActor>();
        _ = actor.QueryAsync(Arg.Any<QueryEnvelope>())
            .ThrowsAsync(new ActorMethodInvocationException("projection query failed", new InvalidOperationException("boom"), false));

        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        _ = factory.CreateActorProxy<IProjectionActor>(Arg.Any<ActorId>(), Arg.Any<string>())
            .Returns(actor);

        var router = new QueryRouter(factory, NullLogger<QueryRouter>.Instance);

        // Act
        QueryRouterResult result = await router.RouteQueryAsync(CreateTestQuery());

        // Assert — fail-closed: exceptions map to ActorException category, never propagate
        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldBe(QueryAdapterFailureReason.ActorException);
    }

    [Fact]
    public async Task RouteQueryAsync_ProjectionActorReturnsFailure_ReturnsFailedResult() {
        // Arrange
        _ = JsonDocument.Parse("{}").RootElement;
        IProjectionActor actor = Substitute.For<IProjectionActor>();
        _ = actor.QueryAsync(Arg.Any<QueryEnvelope>()).Returns(QueryResult.Failure("projection unavailable"));

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
    public async Task RouteQueryAsync_GenericNotRegisteredMessage_ReturnsActorExceptionCategory() {
        // Arrange
        IProjectionActor actor = Substitute.For<IProjectionActor>();
        _ = actor.QueryAsync(Arg.Any<QueryEnvelope>())
            .ThrowsAsync(new ActorMethodInvocationException("serializer dependency not registered", new InvalidOperationException("boom"), false));

        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        _ = factory.CreateActorProxy<IProjectionActor>(Arg.Any<ActorId>(), Arg.Any<string>())
            .Returns(actor);

        var router = new QueryRouter(factory, NullLogger<QueryRouter>.Instance);

        // Act
        QueryRouterResult result = await router.RouteQueryAsync(CreateTestQuery());

        // Assert — fail-closed: all non-cancellation exceptions map to ActorException category
        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldBe(QueryAdapterFailureReason.ActorException);
    }

    [Fact]
    public async Task RouteQueryAsync_WithEntityId_RoutesToTier1ActorId() {
        // Arrange
        JsonElement resultPayload = JsonDocument.Parse("{}").RootElement;
        IProjectionActor actor = Substitute.For<IProjectionActor>();
        _ = actor.QueryAsync(Arg.Any<QueryEnvelope>()).Returns(QueryResult.FromPayload(resultPayload));

        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        _ = factory.CreateActorProxy<IProjectionActor>(Arg.Any<ActorId>(), Arg.Any<string>())
            .Returns(actor);

        var router = new QueryRouter(factory, NullLogger<QueryRouter>.Instance);

        // Act
        _ = await router.RouteQueryAsync(CreateTestQuery(entityId: "order-123"));

        // Assert — Tier 1: {QueryType}:{TenantId}:{EntityId}
        _ = factory.Received(1).CreateActorProxy<IProjectionActor>(
            Arg.Is<ActorId>(id => id.ToString() == "GetOrderStatus:test-tenant:order-123"),
            QueryRouter.ProjectionActorTypeName);
    }

    [Fact]
    public async Task RouteQueryAsync_WithNonEmptyPayloadAndNoEntityId_RoutesToTier2ActorId() {
        // Arrange
        JsonElement resultPayload = JsonDocument.Parse("{}").RootElement;
        IProjectionActor actor = Substitute.For<IProjectionActor>();
        _ = actor.QueryAsync(Arg.Any<QueryEnvelope>()).Returns(QueryResult.FromPayload(resultPayload));

        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        _ = factory.CreateActorProxy<IProjectionActor>(Arg.Any<ActorId>(), Arg.Any<string>())
            .Returns(actor);

        var router = new QueryRouter(factory, NullLogger<QueryRouter>.Instance);
        byte[] payload = [0x01, 0x02, 0x03];

        // Act
        _ = await router.RouteQueryAsync(CreateTestQuery(payload: payload));

        // Assert — Tier 2: {QueryType}:{TenantId}:{Checksum} (11-char checksum)
        _ = factory.Received(1).CreateActorProxy<IProjectionActor>(
            Arg.Is<ActorId>(id => id.ToString().StartsWith("GetOrderStatus:test-tenant:") && id.ToString().Split(':')[2].Length == 11),
            QueryRouter.ProjectionActorTypeName);
    }

    [Fact]
    public async Task RouteQueryAsync_WithEmptyPayloadAndNoEntityId_RoutesToTier3ActorId() {
        // Arrange
        JsonElement resultPayload = JsonDocument.Parse("{}").RootElement;
        IProjectionActor actor = Substitute.For<IProjectionActor>();
        _ = actor.QueryAsync(Arg.Any<QueryEnvelope>()).Returns(QueryResult.FromPayload(resultPayload));

        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        _ = factory.CreateActorProxy<IProjectionActor>(Arg.Any<ActorId>(), Arg.Any<string>())
            .Returns(actor);

        var router = new QueryRouter(factory, NullLogger<QueryRouter>.Instance);

        // Act
        _ = await router.RouteQueryAsync(CreateTestQuery());

        // Assert — Tier 3: {QueryType}:{TenantId}
        _ = factory.Received(1).CreateActorProxy<IProjectionActor>(
            Arg.Is<ActorId>(id => id.ToString() == "GetOrderStatus:test-tenant"),
            QueryRouter.ProjectionActorTypeName);
    }

    [Fact]
    public async Task RouteQueryAsync_ProjectionType_PassesThroughFromQueryResult() {
        // Arrange
        JsonElement resultPayload = JsonDocument.Parse("{\"status\":\"shipped\"}").RootElement;
        var queryResult = QueryResult.FromPayload(resultPayload, "order-list");

        IProjectionActor actor = Substitute.For<IProjectionActor>();
        _ = actor.QueryAsync(Arg.Any<QueryEnvelope>()).Returns(queryResult);

        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        _ = factory.CreateActorProxy<IProjectionActor>(Arg.Any<ActorId>(), Arg.Any<string>())
            .Returns(actor);

        var router = new QueryRouter(factory, NullLogger<QueryRouter>.Instance);

        // Act
        QueryRouterResult result = await router.RouteQueryAsync(CreateTestQuery());

        // Assert — ProjectionType passes through
        result.Success.ShouldBeTrue();
        result.ProjectionType.ShouldBe("order-list");
    }

    [Fact]
    public async Task RouteQueryAsync_NullProjectionType_PassesThroughNull() {
        // Arrange
        JsonElement resultPayload = JsonDocument.Parse("{}").RootElement;
        var queryResult = QueryResult.FromPayload(resultPayload);

        IProjectionActor actor = Substitute.For<IProjectionActor>();
        _ = actor.QueryAsync(Arg.Any<QueryEnvelope>()).Returns(queryResult);

        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        _ = factory.CreateActorProxy<IProjectionActor>(Arg.Any<ActorId>(), Arg.Any<string>())
            .Returns(actor);

        var router = new QueryRouter(factory, NullLogger<QueryRouter>.Instance);

        // Act
        QueryRouterResult result = await router.RouteQueryAsync(CreateTestQuery());

        // Assert
        result.ProjectionType.ShouldBeNull();
    }

    [Fact]
    public async Task RouteQueryAsync_ConstructsCorrectQueryEnvelope() {
        // Arrange
        JsonElement resultPayload = JsonDocument.Parse("{}").RootElement;
        IProjectionActor actor = Substitute.For<IProjectionActor>();
        _ = actor.QueryAsync(Arg.Any<QueryEnvelope>()).Returns(QueryResult.FromPayload(resultPayload));

        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        _ = factory.CreateActorProxy<IProjectionActor>(Arg.Any<ActorId>(), Arg.Any<string>())
            .Returns(actor);

        var router = new QueryRouter(factory, NullLogger<QueryRouter>.Instance);
        SubmitQuery query = CreateTestQuery();

        // Act
        _ = await router.RouteQueryAsync(query);

        // Assert — verify QueryEnvelope fields match SubmitQuery fields
        _ = await actor.Received(1).QueryAsync(Arg.Is<QueryEnvelope>(e =>
            e.TenantId == "test-tenant" &&
            e.Domain == "orders" &&
            e.AggregateId == "order-1" &&
            e.QueryType == "GetOrderStatus" &&
            e.CorrelationId == "corr-1" &&
            e.UserId == "user-1" &&
            e.EntityId == null));
    }

    [Fact]
    public async Task RouteQueryAsync_GetParty_UsesEntityScopedPublicAdapterRoute() {
        JsonElement resultPayload = JsonDocument.Parse("{\"id\":\"party-42\"}").RootElement;
        IProjectionActor actor = Substitute.For<IProjectionActor>();
        _ = actor.QueryAsync(Arg.Any<QueryEnvelope>()).Returns(QueryResult.FromPayload(resultPayload, "party"));

        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        _ = factory.CreateActorProxy<IProjectionActor>(Arg.Any<ActorId>(), Arg.Any<string>())
            .Returns(actor);

        var router = new QueryRouter(factory, NullLogger<QueryRouter>.Instance);

        _ = await router.RouteQueryAsync(CreateTestQuery(
            queryType: "get-party",
            domain: "parties",
            aggregateId: "party",
            entityId: "party-42",
            projectionType: "party"));

        _ = factory.Received(1).CreateActorProxy<IProjectionActor>(
            Arg.Is<ActorId>(id => id.ToString() == "party:test-tenant:party-42"),
            QueryRouter.ProjectionActorTypeName);
        _ = await actor.Received(1).QueryAsync(Arg.Is<QueryEnvelope>(e =>
            e.QueryType == "get-party" &&
            e.Domain == "parties" &&
            e.EntityId == "party-42"));
    }

    [Fact]
    public async Task RouteQueryAsync_ListParties_UsesTenantWideRouteAndActorTypeOverride() {
        JsonElement resultPayload = JsonDocument.Parse("{\"items\":[]}").RootElement;
        IProjectionActor actor = Substitute.For<IProjectionActor>();
        _ = actor.QueryAsync(Arg.Any<QueryEnvelope>()).Returns(QueryResult.FromPayload(resultPayload, "party-list"));

        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        _ = factory.CreateActorProxy<IProjectionActor>(Arg.Any<ActorId>(), Arg.Any<string>())
            .Returns(actor);

        var router = new QueryRouter(factory, NullLogger<QueryRouter>.Instance);

        _ = await router.RouteQueryAsync(CreateTestQuery(
            queryType: "list-parties",
            domain: "parties",
            aggregateId: "party",
            projectionType: "party-list",
            projectionActorType: "PartiesProjectionActor"));

        _ = factory.Received(1).CreateActorProxy<IProjectionActor>(
            Arg.Is<ActorId>(id => id.ToString() == "party-list:test-tenant"),
            "PartiesProjectionActor");
    }

    [Fact]
    public async Task RouteQueryAsync_SearchParties_UsesPayloadChecksumRouteWithoutPayloadData() {
        JsonElement resultPayload = JsonDocument.Parse("{\"items\":[]}").RootElement;
        IProjectionActor actor = Substitute.For<IProjectionActor>();
        _ = actor.QueryAsync(Arg.Any<QueryEnvelope>()).Returns(QueryResult.FromPayload(resultPayload, "party-search"));

        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        _ = factory.CreateActorProxy<IProjectionActor>(Arg.Any<ActorId>(), Arg.Any<string>())
            .Returns(actor);

        var router = new QueryRouter(factory, NullLogger<QueryRouter>.Instance);
        byte[] payload = [1, 2, 3];

        _ = await router.RouteQueryAsync(CreateTestQuery(
            queryType: "search-parties",
            domain: "parties",
            aggregateId: "party",
            payload: payload,
            projectionType: "party-search"));

        _ = factory.Received(1).CreateActorProxy<IProjectionActor>(
            Arg.Is<ActorId>(id => id.ToString() == "party-search:test-tenant:A5BYxvLAy0k"),
            QueryRouter.ProjectionActorTypeName);
        _ = await actor.Received(1).QueryAsync(Arg.Is<QueryEnvelope>(e =>
            e.QueryType == "search-parties" &&
            e.Payload.SequenceEqual(payload)));
    }

    [Fact]
    public async Task RouteQueryAsync_SuccessWithoutPayload_ReturnsMissingPayloadCategory() {
        IProjectionActor actor = Substitute.For<IProjectionActor>();
        _ = actor.QueryAsync(Arg.Any<QueryEnvelope>()).Returns(new QueryResult(true));

        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        _ = factory.CreateActorProxy<IProjectionActor>(Arg.Any<ActorId>(), Arg.Any<string>())
            .Returns(actor);

        var router = new QueryRouter(factory, NullLogger<QueryRouter>.Instance);

        QueryRouterResult result = await router.RouteQueryAsync(CreateTestQuery());

        result.Success.ShouldBeFalse();
        result.NotFound.ShouldBeFalse();
        result.Payload.ShouldBeNull();
        result.ErrorMessage.ShouldBe(QueryAdapterFailureReason.MissingPayload);
    }

    [Fact]
    public async Task RouteQueryAsync_InvalidPayloadBytes_ReturnsSerializationFailureCategory() {
        IProjectionActor actor = Substitute.For<IProjectionActor>();
        _ = actor.QueryAsync(Arg.Any<QueryEnvelope>()).Returns(new QueryResult(true, [0xFF]));

        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        _ = factory.CreateActorProxy<IProjectionActor>(Arg.Any<ActorId>(), Arg.Any<string>())
            .Returns(actor);

        var router = new QueryRouter(factory, NullLogger<QueryRouter>.Instance);

        QueryRouterResult result = await router.RouteQueryAsync(CreateTestQuery());

        result.Success.ShouldBeFalse();
        result.NotFound.ShouldBeFalse();
        result.Payload.ShouldBeNull();
        result.ErrorMessage.ShouldBe(QueryAdapterFailureReason.SerializationFailure);
    }

    [Fact]
    public async Task RouteQueryAsync_NullActorResult_ReturnsActorResponseMismatchCategory() {
        IProjectionActor actor = Substitute.For<IProjectionActor>();
        _ = actor.QueryAsync(Arg.Any<QueryEnvelope>()).Returns((QueryResult)null!);

        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        _ = factory.CreateActorProxy<IProjectionActor>(Arg.Any<ActorId>(), Arg.Any<string>())
            .Returns(actor);

        var router = new QueryRouter(factory, NullLogger<QueryRouter>.Instance);

        QueryRouterResult result = await router.RouteQueryAsync(CreateTestQuery());

        result.Success.ShouldBeFalse();
        result.NotFound.ShouldBeFalse();
        result.Payload.ShouldBeNull();
        result.ErrorMessage.ShouldBe(QueryAdapterFailureReason.ActorResponseMismatch);
    }

    [Fact]
    public async Task RouteQueryAsync_ActorMethodInvocationException_ReturnsActorExceptionCategory() {
        IProjectionActor actor = Substitute.For<IProjectionActor>();
        _ = actor.QueryAsync(Arg.Any<QueryEnvelope>())
            .Throws(new ActorMethodInvocationException("actor invocation failed", new InvalidOperationException("inner"), false));

        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        _ = factory.CreateActorProxy<IProjectionActor>(Arg.Any<ActorId>(), Arg.Any<string>())
            .Returns(actor);

        var router = new QueryRouter(factory, NullLogger<QueryRouter>.Instance);

        QueryRouterResult result = await router.RouteQueryAsync(CreateTestQuery());

        result.Success.ShouldBeFalse();
        result.NotFound.ShouldBeFalse();
        result.Payload.ShouldBeNull();
        result.ErrorMessage.ShouldBe(QueryAdapterFailureReason.ActorException);
    }

    [Fact]
    public async Task RouteQueryAsync_GenericException_ReturnsActorExceptionCategory() {
        IProjectionActor actor = Substitute.For<IProjectionActor>();
        _ = actor.QueryAsync(Arg.Any<QueryEnvelope>())
            .Throws(new InvalidOperationException("unexpected failure"));

        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        _ = factory.CreateActorProxy<IProjectionActor>(Arg.Any<ActorId>(), Arg.Any<string>())
            .Returns(actor);

        var router = new QueryRouter(factory, NullLogger<QueryRouter>.Instance);

        QueryRouterResult result = await router.RouteQueryAsync(CreateTestQuery());

        result.Success.ShouldBeFalse();
        result.NotFound.ShouldBeFalse();
        result.Payload.ShouldBeNull();
        result.ErrorMessage.ShouldBe(QueryAdapterFailureReason.ActorException);
    }

    [Fact]
    public async Task RouteQueryAsync_CancellationRequested_ThrowsOperationCanceledException() {
        IProjectionActor actor = Substitute.For<IProjectionActor>();
        _ = actor.QueryAsync(Arg.Any<QueryEnvelope>())
            .Throws(new OperationCanceledException());

        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        _ = factory.CreateActorProxy<IProjectionActor>(Arg.Any<ActorId>(), Arg.Any<string>())
            .Returns(actor);

        var router = new QueryRouter(factory, NullLogger<QueryRouter>.Instance);

        await Should.ThrowAsync<OperationCanceledException>(
            () => router.RouteQueryAsync(CreateTestQuery()));
    }

    [Fact]
    public async Task RouteQueryAsync_PreCancelledToken_ThrowsBeforeProxyCreation() {
        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        var router = new QueryRouter(factory, NullLogger<QueryRouter>.Instance);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(
            () => router.RouteQueryAsync(CreateTestQuery(), cts.Token));

        _ = factory.DidNotReceive().CreateActorProxy<IProjectionActor>(
            Arg.Any<ActorId>(),
            Arg.Any<string>());
    }

    [Fact]
    public async Task RouteQueryAsync_OperationCanceledException_IsNotConvertedToAdapterFailure() {
        IProjectionActor actor = Substitute.For<IProjectionActor>();
        _ = actor.QueryAsync(Arg.Any<QueryEnvelope>())
            .Throws(new OperationCanceledException("abandoned request"));

        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        _ = factory.CreateActorProxy<IProjectionActor>(Arg.Any<ActorId>(), Arg.Any<string>())
            .Returns(actor);

        var router = new QueryRouter(factory, NullLogger<QueryRouter>.Instance);

        OperationCanceledException exception = await Should.ThrowAsync<OperationCanceledException>(
            () => router.RouteQueryAsync(CreateTestQuery()));

        exception.Message.ShouldContain("abandoned request");
    }
}
