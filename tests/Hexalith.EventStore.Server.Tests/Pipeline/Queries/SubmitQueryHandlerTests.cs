
using System.Text.Json;

using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Server.Pipeline;
using Hexalith.EventStore.Server.Pipeline.Queries;
using Hexalith.EventStore.Server.Queries;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Pipeline.Queries;

public class SubmitQueryHandlerTests {
    private static SubmitQuery CreateTestQuery() =>
        new(
            Tenant: "test-tenant",
            Domain: "orders",
            AggregateId: "order-1",
            QueryType: "GetOrderStatus",
            Payload: [],
            CorrelationId: "corr-1",
            UserId: "user-1",
            EntityId: null);

    [Fact]
    public async Task Handle_SuccessfulQuery_ReturnsSubmitQueryResult() {
        // Arrange
        JsonElement payload = JsonDocument.Parse("{\"status\":\"shipped\"}").RootElement;
        IQueryRouter router = Substitute.For<IQueryRouter>();
        _ = router.RouteQueryAsync(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(new QueryRouterResult(Success: true, Payload: payload, NotFound: false));

        var handler = new SubmitQueryHandler(router, NullLogger<SubmitQueryHandler>.Instance);
        SubmitQuery query = CreateTestQuery();

        // Act
        SubmitQueryResult result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.CorrelationId.ShouldBe("corr-1");
        result.Payload.GetProperty("status").GetString().ShouldBe("shipped");
    }

    [Fact]
    public async Task Handle_SuccessfulQuery_PassesThroughProjectionType() {
        // Arrange
        JsonElement payload = JsonDocument.Parse("{\"data\":1}").RootElement;
        IQueryRouter router = Substitute.For<IQueryRouter>();
        _ = router.RouteQueryAsync(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(new QueryRouterResult(Success: true, Payload: payload, NotFound: false, ProjectionType: "order-list"));

        var handler = new SubmitQueryHandler(router, NullLogger<SubmitQueryHandler>.Instance);

        // Act
        SubmitQueryResult result = await handler.Handle(CreateTestQuery(), CancellationToken.None);

        // Assert
        result.ProjectionType.ShouldBe("order-list");
    }

    [Fact]
    public async Task Handle_NullProjectionType_PassesThroughNull() {
        // Arrange
        JsonElement payload = JsonDocument.Parse("{\"data\":1}").RootElement;
        IQueryRouter router = Substitute.For<IQueryRouter>();
        _ = router.RouteQueryAsync(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(new QueryRouterResult(Success: true, Payload: payload, NotFound: false, ProjectionType: null));

        var handler = new SubmitQueryHandler(router, NullLogger<SubmitQueryHandler>.Instance);

        // Act
        SubmitQueryResult result = await handler.Handle(CreateTestQuery(), CancellationToken.None);

        // Assert
        result.ProjectionType.ShouldBeNull();
    }

    [Fact]
    public async Task Handle_NotFound_ThrowsQueryNotFoundException() {
        // Arrange
        IQueryRouter router = Substitute.For<IQueryRouter>();
        _ = router.RouteQueryAsync(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(new QueryRouterResult(Success: false, Payload: null, NotFound: true));

        var handler = new SubmitQueryHandler(router, NullLogger<SubmitQueryHandler>.Instance);
        SubmitQuery query = CreateTestQuery();

        // Act & Assert
        QueryNotFoundException ex = await Should.ThrowAsync<QueryNotFoundException>(
            () => handler.Handle(query, CancellationToken.None));

        ex.Tenant.ShouldBe("test-tenant");
        ex.Domain.ShouldBe("orders");
        ex.AggregateId.ShouldBe("order-1");
        ex.QueryType.ShouldBe("GetOrderStatus");
    }

    [Fact]
    public async Task Handle_RouterReturnsFailure_ThrowsQueryExecutionFailedException() {
        // Arrange
        IQueryRouter router = Substitute.For<IQueryRouter>();
        _ = router.RouteQueryAsync(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(new QueryRouterResult(Success: false, Payload: null, NotFound: false, ErrorMessage: "projection unavailable"));

        var handler = new SubmitQueryHandler(router, NullLogger<SubmitQueryHandler>.Instance);

        // Act & Assert
        QueryExecutionFailedException ex = await Should.ThrowAsync<QueryExecutionFailedException>(
            () => handler.Handle(CreateTestQuery(), CancellationToken.None));

        ex.StatusCode.ShouldBe(500);
        ex.Detail.ShouldBe("projection unavailable");
    }

    [Theory]
    [InlineData(QueryAdapterFailureReason.MissingPayload)]
    [InlineData(QueryAdapterFailureReason.SerializationFailure)]
    [InlineData(QueryAdapterFailureReason.ActorResponseMismatch)]
    [InlineData(QueryAdapterFailureReason.ActorException)]
    public async Task Handle_AdapterEdgeFailureReason_ThrowsQueryExecutionFailedException(string failureReason) {
        // Arrange
        IQueryRouter router = Substitute.For<IQueryRouter>();
        _ = router.RouteQueryAsync(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(new QueryRouterResult(Success: false, Payload: null, NotFound: false, ErrorMessage: failureReason));

        var handler = new SubmitQueryHandler(router, NullLogger<SubmitQueryHandler>.Instance);

        // Act & Assert
        QueryExecutionFailedException ex = await Should.ThrowAsync<QueryExecutionFailedException>(
            () => handler.Handle(CreateTestQuery(), CancellationToken.None));

        ex.StatusCode.ShouldBe(500);
        ex.Detail.ShouldBe(failureReason);
    }

    [Theory]
    [InlineData(QueryAdapterFailureReason.UnsupportedQueryType)]
    [InlineData(QueryAdapterFailureReason.UnknownQueryType)]
    public async Task Handle_NotImplementedFailureReason_ThrowsQueryExecutionFailedExceptionWith501(string failureReason) {
        // Arrange
        IQueryRouter router = Substitute.For<IQueryRouter>();
        _ = router.RouteQueryAsync(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(new QueryRouterResult(Success: false, Payload: null, NotFound: false, ErrorMessage: failureReason));

        var handler = new SubmitQueryHandler(router, NullLogger<SubmitQueryHandler>.Instance);

        // Act & Assert
        QueryExecutionFailedException ex = await Should.ThrowAsync<QueryExecutionFailedException>(
            () => handler.Handle(CreateTestQuery(), CancellationToken.None));

        ex.StatusCode.ShouldBe(501);
        ex.Detail.ShouldBe(failureReason);
    }

    [Fact]
    public async Task Handle_ForbiddenFailureReason_ThrowsQueryExecutionFailedExceptionWith403() {
        // Arrange
        IQueryRouter router = Substitute.For<IQueryRouter>();
        _ = router.RouteQueryAsync(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(new QueryRouterResult(Success: false, Payload: null, NotFound: false, ErrorMessage: QueryAdapterFailureReason.Forbidden));

        var handler = new SubmitQueryHandler(router, NullLogger<SubmitQueryHandler>.Instance);

        // Act & Assert
        QueryExecutionFailedException ex = await Should.ThrowAsync<QueryExecutionFailedException>(
            () => handler.Handle(CreateTestQuery(), CancellationToken.None));

        ex.StatusCode.ShouldBe(403);
        ex.Detail.ShouldBe(QueryAdapterFailureReason.Forbidden);
    }

    [Fact]
    public async Task Handle_CancellationRequested_PropagatesOperationCanceledException() {
        // Arrange
        using var cts = new CancellationTokenSource();
        IQueryRouter router = Substitute.For<IQueryRouter>();
        _ = router.RouteQueryAsync(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());

        var handler = new SubmitQueryHandler(router, NullLogger<SubmitQueryHandler>.Instance);

        // Act & Assert
        await Should.ThrowAsync<OperationCanceledException>(
            () => handler.Handle(CreateTestQuery(), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_MissingProjectionState_ThrowsQueryNotFoundException() {
        // Arrange
        IQueryRouter router = Substitute.For<IQueryRouter>();
        _ = router.RouteQueryAsync(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(new QueryRouterResult(
                Success: false,
                Payload: null,
                NotFound: false,
                ErrorMessage: "No projection state available for this aggregate"));

        var handler = new SubmitQueryHandler(router, NullLogger<SubmitQueryHandler>.Instance);

        // Act & Assert
        QueryNotFoundException ex = await Should.ThrowAsync<QueryNotFoundException>(
            () => handler.Handle(CreateTestQuery(), CancellationToken.None));

        ex.Tenant.ShouldBe("test-tenant");
        ex.Domain.ShouldBe("orders");
        ex.AggregateId.ShouldBe("order-1");
        ex.QueryType.ShouldBe("GetOrderStatus");
    }

    [Fact]
    public async Task Handle_SuccessWithoutPayload_ThrowsInvalidOperationException() {
        // Arrange
        IQueryRouter router = Substitute.For<IQueryRouter>();
        _ = router.RouteQueryAsync(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(new QueryRouterResult(Success: true, Payload: null, NotFound: false));

        var handler = new SubmitQueryHandler(router, NullLogger<SubmitQueryHandler>.Instance);

        // Act & Assert
        InvalidOperationException ex = await Should.ThrowAsync<InvalidOperationException>(
            () => handler.Handle(CreateTestQuery(), CancellationToken.None));

        ex.Message.ShouldBe("Projection query completed without a payload.");
    }

    [Fact]
    public async Task Handle_RouterException_Propagates() {
        // Arrange
        IQueryRouter router = Substitute.For<IQueryRouter>();
        _ = router.RouteQueryAsync(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("boom"));

        var handler = new SubmitQueryHandler(router, NullLogger<SubmitQueryHandler>.Instance);

        // Act & Assert
        _ = await Should.ThrowAsync<InvalidOperationException>(
            () => handler.Handle(CreateTestQuery(), CancellationToken.None));
    }
}
