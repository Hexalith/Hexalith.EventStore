
using System.Security.Claims;
using System.Text.Json;

using Hexalith.EventStore.CommandApi.Controllers;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Server.Pipeline.Queries;

using MediatR;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Controllers;

public class QueriesControllerTests {
    private static SubmitQueryRequest CreateTestRequest(JsonElement? payload = null) =>
        new(
            Tenant: "test-tenant",
            Domain: "orders",
            AggregateId: "order-1",
            QueryType: "GetOrderStatus",
            Payload: payload);

    private static QueriesController CreateController(IMediator mediator, ClaimsPrincipal? principal = null) {
        var controller = new QueriesController(mediator, NullLogger<QueriesController>.Instance);
        var httpContext = new DefaultHttpContext {
            User = principal ?? new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("sub", "test-user")], "test")),
        };
        httpContext.Items["CorrelationId"] = "corr-1";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        return controller;
    }

    [Fact]
    public async Task Submit_ValidRequest_Returns200WithPayload() {
        // Arrange
        JsonElement resultPayload = JsonDocument.Parse("{\"status\":\"shipped\"}").RootElement;
        IMediator mediator = Substitute.For<IMediator>();
        _ = mediator.Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(new SubmitQueryResult("corr-1", resultPayload));

        QueriesController controller = CreateController(mediator);
        SubmitQueryRequest request = CreateTestRequest();

        // Act
        IActionResult actionResult = await controller.Submit(request, CancellationToken.None);

        // Assert
        OkObjectResult okResult = actionResult.ShouldBeOfType<OkObjectResult>();
        okResult.StatusCode.ShouldBe(200);
        SubmitQueryResponse response = okResult.Value.ShouldBeOfType<SubmitQueryResponse>();
        response.CorrelationId.ShouldBe("corr-1");
        response.Payload.GetProperty("status").GetString().ShouldBe("shipped");
    }

    [Fact]
    public async Task Submit_CorrelationIdExtractedFromHttpContext() {
        // Arrange
        JsonElement resultPayload = JsonDocument.Parse("{}").RootElement;
        IMediator mediator = Substitute.For<IMediator>();
        _ = mediator.Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(new SubmitQueryResult("corr-1", resultPayload));

        QueriesController controller = CreateController(mediator);

        // Act
        _ = await controller.Submit(CreateTestRequest(), CancellationToken.None);

        // Assert — verify mediator received query with correlationId from HttpContext
        await mediator.Received(1).Send(
            Arg.Is<SubmitQuery>(q => q.CorrelationId == "corr-1"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Submit_StoresRequestTenantIdInHttpContext() {
        // Arrange
        JsonElement resultPayload = JsonDocument.Parse("{}").RootElement;
        IMediator mediator = Substitute.For<IMediator>();
        _ = mediator.Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(new SubmitQueryResult("corr-1", resultPayload));

        QueriesController controller = CreateController(mediator);

        // Act
        _ = await controller.Submit(CreateTestRequest(), CancellationToken.None);

        // Assert
        controller.HttpContext.Items["RequestTenantId"].ShouldBe("test-tenant");
    }

    [Fact]
    public async Task Submit_UserIdExtractedFromJwtSubClaim() {
        // Arrange
        JsonElement resultPayload = JsonDocument.Parse("{}").RootElement;
        IMediator mediator = Substitute.For<IMediator>();
        _ = mediator.Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(new SubmitQueryResult("corr-1", resultPayload));

        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("sub", "jwt-user-id")], "test"));
        QueriesController controller = CreateController(mediator, principal);

        // Act
        _ = await controller.Submit(CreateTestRequest(), CancellationToken.None);

        // Assert
        await mediator.Received(1).Send(
            Arg.Is<SubmitQuery>(q => q.UserId == "jwt-user-id"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Submit_MissingSubClaim_ReturnsUnauthorizedAndDoesNotCallMediator() {
        // Arrange
        IMediator mediator = Substitute.For<IMediator>();
        var principal = new ClaimsPrincipal(new ClaimsIdentity([], "test"));
        QueriesController controller = CreateController(mediator, principal);

        // Act
        IActionResult actionResult = await controller.Submit(CreateTestRequest(), CancellationToken.None);

        // Assert
        actionResult.ShouldBeOfType<UnauthorizedResult>();
        await mediator.DidNotReceive().Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Submit_NullPayload_PassesEmptyByteArray() {
        // Arrange
        JsonElement resultPayload = JsonDocument.Parse("{}").RootElement;
        IMediator mediator = Substitute.For<IMediator>();
        _ = mediator.Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(new SubmitQueryResult("corr-1", resultPayload));

        QueriesController controller = CreateController(mediator);
        SubmitQueryRequest request = CreateTestRequest(payload: null);

        // Act
        _ = await controller.Submit(request, CancellationToken.None);

        // Assert
        await mediator.Received(1).Send(
            Arg.Is<SubmitQuery>(q => q.Payload.Length == 0),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Submit_WithPayload_SerializesToUtf8Bytes() {
        // Arrange
        JsonElement resultPayload = JsonDocument.Parse("{}").RootElement;
        IMediator mediator = Substitute.For<IMediator>();
        _ = mediator.Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(new SubmitQueryResult("corr-1", resultPayload));

        QueriesController controller = CreateController(mediator);
        JsonElement requestPayload = JsonDocument.Parse("{\"filter\":\"active\"}").RootElement;
        SubmitQueryRequest request = CreateTestRequest(payload: requestPayload);

        // Act
        _ = await controller.Submit(request, CancellationToken.None);

        // Assert — payload bytes should be non-empty
        await mediator.Received(1).Send(
            Arg.Is<SubmitQuery>(q => q.Payload.Length > 0),
            Arg.Any<CancellationToken>());
    }
}
