
using System.Security.Claims;
using System.Text.Json;

using Hexalith.EventStore.CommandApi.Controllers;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Server.Pipeline.Queries;
using Hexalith.EventStore.Server.Queries;

using MediatR;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

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

    private static QueriesController CreateController(
        IMediator mediator,
        IETagService? eTagService = null,
        ClaimsPrincipal? principal = null) {
        eTagService ??= Substitute.For<IETagService>();
        var controller = new QueriesController(mediator, eTagService, NullLogger<QueriesController>.Instance);
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
        IActionResult actionResult = await controller.Submit(request, null, CancellationToken.None);

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
        _ = await controller.Submit(CreateTestRequest(), null, CancellationToken.None);

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
        _ = await controller.Submit(CreateTestRequest(), null, CancellationToken.None);

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
        QueriesController controller = CreateController(mediator, principal: principal);

        // Act
        _ = await controller.Submit(CreateTestRequest(), null, CancellationToken.None);

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
        QueriesController controller = CreateController(mediator, principal: principal);

        // Act
        IActionResult actionResult = await controller.Submit(CreateTestRequest(), null, CancellationToken.None);

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
        _ = await controller.Submit(request, null, CancellationToken.None);

        // Assert
        await mediator.Received(1).Send(
            Arg.Is<SubmitQuery>(q => q.Payload.Length == 0),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Submit_WithEntityId_ForwardsEntityIdToSubmitQuery() {
        // Arrange
        JsonElement resultPayload = JsonDocument.Parse("{}").RootElement;
        IMediator mediator = Substitute.For<IMediator>();
        _ = mediator.Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(new SubmitQueryResult("corr-1", resultPayload));

        QueriesController controller = CreateController(mediator);
        var request = new SubmitQueryRequest(
            Tenant: "test-tenant",
            Domain: "orders",
            AggregateId: "order-1",
            QueryType: "GetOrderStatus",
            EntityId: "entity-42");

        // Act
        _ = await controller.Submit(request, null, CancellationToken.None);

        // Assert — EntityId from request forwarded to SubmitQuery
        await mediator.Received(1).Send(
            Arg.Is<SubmitQuery>(q => q.EntityId == "entity-42"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Submit_WithNullEntityId_ForwardsNullEntityIdToSubmitQuery() {
        // Arrange
        JsonElement resultPayload = JsonDocument.Parse("{}").RootElement;
        IMediator mediator = Substitute.For<IMediator>();
        _ = mediator.Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(new SubmitQueryResult("corr-1", resultPayload));

        QueriesController controller = CreateController(mediator);

        // Act
        _ = await controller.Submit(CreateTestRequest(), null, CancellationToken.None);

        // Assert — null EntityId from request results in null EntityId in SubmitQuery
        await mediator.Received(1).Send(
            Arg.Is<SubmitQuery>(q => q.EntityId == null),
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
        _ = await controller.Submit(request, null, CancellationToken.None);

        // Assert — payload bytes should be non-empty
        await mediator.Received(1).Send(
            Arg.Is<SubmitQuery>(q => q.Payload.Length > 0),
            Arg.Any<CancellationToken>());
    }

    // ===== Gate 1: ETag pre-check tests =====

    [Fact]
    public async Task Submit_IfNoneMatchMatches_Returns304() {
        // Arrange
        IMediator mediator = Substitute.For<IMediator>();
        IETagService eTagService = Substitute.For<IETagService>();
        _ = eTagService.GetCurrentETagAsync("orders", "test-tenant", Arg.Any<CancellationToken>())
            .Returns("abc123etag");

        QueriesController controller = CreateController(mediator, eTagService);

        // Act
        IActionResult actionResult = await controller.Submit(CreateTestRequest(), "\"abc123etag\"", CancellationToken.None);

        // Assert
        StatusCodeResult statusResult = actionResult.ShouldBeOfType<StatusCodeResult>();
        statusResult.StatusCode.ShouldBe(304);
        await mediator.DidNotReceive().Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Submit_IfNoneMatchDoesNotMatch_Returns200WithETagHeader() {
        // Arrange
        JsonElement resultPayload = JsonDocument.Parse("{\"data\":1}").RootElement;
        IMediator mediator = Substitute.For<IMediator>();
        _ = mediator.Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(new SubmitQueryResult("corr-1", resultPayload));
        IETagService eTagService = Substitute.For<IETagService>();
        _ = eTagService.GetCurrentETagAsync("orders", "test-tenant", Arg.Any<CancellationToken>())
            .Returns("new-etag");

        QueriesController controller = CreateController(mediator, eTagService);

        // Act
        IActionResult actionResult = await controller.Submit(CreateTestRequest(), "\"old-etag\"", CancellationToken.None);

        // Assert
        OkObjectResult okResult = actionResult.ShouldBeOfType<OkObjectResult>();
        okResult.StatusCode.ShouldBe(200);
        controller.Response.Headers.ETag.ToString().ShouldBe("\"new-etag\"");
    }

    [Fact]
    public async Task Submit_NoIfNoneMatchHeader_Returns200WithETagHeader() {
        // Arrange
        JsonElement resultPayload = JsonDocument.Parse("{\"data\":1}").RootElement;
        IMediator mediator = Substitute.For<IMediator>();
        _ = mediator.Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(new SubmitQueryResult("corr-1", resultPayload));
        IETagService eTagService = Substitute.For<IETagService>();
        _ = eTagService.GetCurrentETagAsync("orders", "test-tenant", Arg.Any<CancellationToken>())
            .Returns("current-etag");

        QueriesController controller = CreateController(mediator, eTagService);

        // Act
        IActionResult actionResult = await controller.Submit(CreateTestRequest(), null, CancellationToken.None);

        // Assert
        actionResult.ShouldBeOfType<OkObjectResult>();
        controller.Response.Headers.ETag.ToString().ShouldBe("\"current-etag\"");
    }

    [Fact]
    public async Task Submit_NullETagColdStart_Returns200WithoutETagHeader() {
        // Arrange
        JsonElement resultPayload = JsonDocument.Parse("{\"data\":1}").RootElement;
        IMediator mediator = Substitute.For<IMediator>();
        _ = mediator.Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(new SubmitQueryResult("corr-1", resultPayload));
        IETagService eTagService = Substitute.For<IETagService>();
        _ = eTagService.GetCurrentETagAsync("orders", "test-tenant", Arg.Any<CancellationToken>())
            .Returns((string?)null);

        QueriesController controller = CreateController(mediator, eTagService);

        // Act
        IActionResult actionResult = await controller.Submit(CreateTestRequest(), null, CancellationToken.None);

        // Assert
        actionResult.ShouldBeOfType<OkObjectResult>();
        controller.Response.Headers.ETag.ToString().ShouldBeEmpty();
    }

    [Fact]
    public async Task Submit_MultipleETagsOneMatches_Returns304() {
        // Arrange
        IMediator mediator = Substitute.For<IMediator>();
        IETagService eTagService = Substitute.For<IETagService>();
        _ = eTagService.GetCurrentETagAsync("orders", "test-tenant", Arg.Any<CancellationToken>())
            .Returns("etag-b");

        QueriesController controller = CreateController(mediator, eTagService);

        // Act
        IActionResult actionResult = await controller.Submit(
            CreateTestRequest(), "\"etag-a\", \"etag-b\", \"etag-c\"", CancellationToken.None);

        // Assert
        StatusCodeResult statusResult = actionResult.ShouldBeOfType<StatusCodeResult>();
        statusResult.StatusCode.ShouldBe(304);
    }

    [Fact]
    public async Task Submit_MultipleETagsNoneMatch_Returns200() {
        // Arrange
        JsonElement resultPayload = JsonDocument.Parse("{}").RootElement;
        IMediator mediator = Substitute.For<IMediator>();
        _ = mediator.Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(new SubmitQueryResult("corr-1", resultPayload));
        IETagService eTagService = Substitute.For<IETagService>();
        _ = eTagService.GetCurrentETagAsync("orders", "test-tenant", Arg.Any<CancellationToken>())
            .Returns("etag-x");

        QueriesController controller = CreateController(mediator, eTagService);

        // Act
        IActionResult actionResult = await controller.Submit(
            CreateTestRequest(), "\"etag-a\", \"etag-b\"", CancellationToken.None);

        // Assert
        actionResult.ShouldBeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Submit_WildcardIfNoneMatch_Returns304() {
        // Arrange
        IMediator mediator = Substitute.For<IMediator>();
        IETagService eTagService = Substitute.For<IETagService>();
        _ = eTagService.GetCurrentETagAsync("orders", "test-tenant", Arg.Any<CancellationToken>())
            .Returns("some-etag");

        QueriesController controller = CreateController(mediator, eTagService);

        // Act
        IActionResult actionResult = await controller.Submit(CreateTestRequest(), "*", CancellationToken.None);

        // Assert
        StatusCodeResult statusResult = actionResult.ShouldBeOfType<StatusCodeResult>();
        statusResult.StatusCode.ShouldBe(304);
    }

    [Fact]
    public async Task Submit_ETagServiceReturnsNull_ColdStartProceeds200() {
        // Arrange — ETag service returns null (cold start or failure)
        JsonElement resultPayload = JsonDocument.Parse("{}").RootElement;
        IMediator mediator = Substitute.For<IMediator>();
        _ = mediator.Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(new SubmitQueryResult("corr-1", resultPayload));
        IETagService eTagService = Substitute.For<IETagService>();
        _ = eTagService.GetCurrentETagAsync("orders", "test-tenant", Arg.Any<CancellationToken>())
            .Returns((string?)null);

        QueriesController controller = CreateController(mediator, eTagService);

        // Act — even with If-None-Match, null ETag means no 304
        IActionResult actionResult = await controller.Submit(CreateTestRequest(), "\"some-etag\"", CancellationToken.None);

        // Assert
        actionResult.ShouldBeOfType<OkObjectResult>();
        await mediator.Received(1).Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Submit_ETagHeaderFormat_DoubleQuoted() {
        // Arrange
        JsonElement resultPayload = JsonDocument.Parse("{}").RootElement;
        IMediator mediator = Substitute.For<IMediator>();
        _ = mediator.Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(new SubmitQueryResult("corr-1", resultPayload));
        IETagService eTagService = Substitute.For<IETagService>();
        _ = eTagService.GetCurrentETagAsync("orders", "test-tenant", Arg.Any<CancellationToken>())
            .Returns("abc123");

        QueriesController controller = CreateController(mediator, eTagService);

        // Act
        _ = await controller.Submit(CreateTestRequest(), null, CancellationToken.None);

        // Assert — RFC 7232: ETag header must be double-quoted
        string eTagHeader = controller.Response.Headers.ETag.ToString();
        eTagHeader.ShouldStartWith("\"");
        eTagHeader.ShouldEndWith("\"");
        eTagHeader.ShouldBe("\"abc123\"");
    }

    [Fact]
    public async Task Submit_MoreThan10ETagValues_SkipsGate1Returns200() {
        // Arrange — 11 ETags, one matching — Gate 1 should skip (PM-10)
        JsonElement resultPayload = JsonDocument.Parse("{}").RootElement;
        IMediator mediator = Substitute.For<IMediator>();
        _ = mediator.Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(new SubmitQueryResult("corr-1", resultPayload));
        IETagService eTagService = Substitute.For<IETagService>();
        _ = eTagService.GetCurrentETagAsync("orders", "test-tenant", Arg.Any<CancellationToken>())
            .Returns("etag-5");

        QueriesController controller = CreateController(mediator, eTagService);

        string manyETags = string.Join(", ", Enumerable.Range(1, 11).Select(i => $"\"etag-{i}\""));

        // Act
        IActionResult actionResult = await controller.Submit(CreateTestRequest(), manyETags, CancellationToken.None);

        // Assert — should proceed to full query despite containing a match
        actionResult.ShouldBeOfType<OkObjectResult>();
    }
}
