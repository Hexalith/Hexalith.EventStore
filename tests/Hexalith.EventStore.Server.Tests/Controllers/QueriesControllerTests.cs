
using System.Diagnostics;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Controllers;
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
    /// <summary>
    /// Generates a self-routing ETag for the "orders" projection type (matches CreateTestRequest).
    /// </summary>
    private static string GenerateTestETag(string projectionType = "orders") =>
        SelfRoutingETag.GenerateNew(projectionType);

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

    private static HeaderProjectionTypeAnalysis AnalyzeHeader(string ifNoneMatch) {
        MethodInfo method = typeof(QueriesController).GetMethod(
            "AnalyzeHeaderProjectionTypes",
            BindingFlags.Static | BindingFlags.NonPublic)!;

        object result = method.Invoke(null, [ifNoneMatch])!;
        PropertyInfo projectionTypeProperty = result.GetType().GetProperty("ProjectionType")!;
        PropertyInfo mixedProperty = result.GetType().GetProperty("HasMixedProjectionTypes")!;

        return new HeaderProjectionTypeAnalysis(
            (string?)projectionTypeProperty.GetValue(result),
            (bool)mixedProperty.GetValue(result)!);
    }

    private readonly record struct HeaderProjectionTypeAnalysis(string? ProjectionType, bool HasMixedProjectionTypes);

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
        _ = await mediator.Received(1).Send(
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
        _ = await mediator.Received(1).Send(
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
        _ = actionResult.ShouldBeOfType<UnauthorizedResult>();
        _ = await mediator.DidNotReceive().Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>());
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
        _ = await mediator.Received(1).Send(
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
        _ = await mediator.Received(1).Send(
            Arg.Is<SubmitQuery>(q => q.EntityId == "entity-42"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Submit_WithNullEntityId_FallsBackToAggregateIdForRouting() {
        // Arrange
        JsonElement resultPayload = JsonDocument.Parse("{}").RootElement;
        IMediator mediator = Substitute.For<IMediator>();
        _ = mediator.Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(new SubmitQueryResult("corr-1", resultPayload));

        QueriesController controller = CreateController(mediator);

        // Act
        _ = await controller.Submit(CreateTestRequest(), null, CancellationToken.None);

        // Assert — aggregate-scoped queries fall back to AggregateId when EntityId is omitted
        _ = await mediator.Received(1).Send(
            Arg.Is<SubmitQuery>(q => q.EntityId == "order-1"),
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
        _ = await mediator.Received(1).Send(
            Arg.Is<SubmitQuery>(q => q.Payload.Length > 0),
            Arg.Any<CancellationToken>());
    }

    // ===== Gate 1: ETag pre-check tests =====

    [Fact]
    public async Task Submit_IfNoneMatchMatches_Returns304() {
        // Arrange — use self-routing ETag so decode succeeds
        string testETag = GenerateTestETag();
        IMediator mediator = Substitute.For<IMediator>();
        IETagService eTagService = Substitute.For<IETagService>();
        _ = eTagService.GetCurrentETagAsync("orders", "test-tenant", Arg.Any<CancellationToken>())
            .Returns(testETag);

        QueriesController controller = CreateController(mediator, eTagService);

        // Act
        IActionResult actionResult = await controller.Submit(CreateTestRequest(), $"\"{testETag}\"", CancellationToken.None);

        // Assert
        StatusCodeResult statusResult = actionResult.ShouldBeOfType<StatusCodeResult>();
        statusResult.StatusCode.ShouldBe(304);
        controller.Response.Headers.ETag.ToString().ShouldBe($"\"{testETag}\"");
        _ = await mediator.DidNotReceive().Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Submit_IfNoneMatchDoesNotMatch_Returns200WithETagHeader() {
        // Arrange — client sends old self-routing ETag, server has newer one
        string clientETag = GenerateTestETag();
        string serverETag = GenerateTestETag();
        JsonElement resultPayload = JsonDocument.Parse("{\"data\":1}").RootElement;
        IMediator mediator = Substitute.For<IMediator>();
        _ = mediator.Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(new SubmitQueryResult("corr-1", resultPayload));
        IETagService eTagService = Substitute.For<IETagService>();
        _ = eTagService.GetCurrentETagAsync("orders", "test-tenant", Arg.Any<CancellationToken>())
            .Returns(serverETag);

        QueriesController controller = CreateController(mediator, eTagService);

        // Act
        IActionResult actionResult = await controller.Submit(CreateTestRequest(), $"\"{clientETag}\"", CancellationToken.None);

        // Assert
        OkObjectResult okResult = actionResult.ShouldBeOfType<OkObjectResult>();
        okResult.StatusCode.ShouldBe(200);
        controller.Response.Headers.ETag.ToString().ShouldBe($"\"{serverETag}\"");
    }

    [Fact]
    public async Task Submit_NoIfNoneMatchHeader_Returns200WithETagHeader() {
        // Arrange
        string serverETag = GenerateTestETag();
        JsonElement resultPayload = JsonDocument.Parse("{\"data\":1}").RootElement;
        IMediator mediator = Substitute.For<IMediator>();
        _ = mediator.Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(new SubmitQueryResult("corr-1", resultPayload));
        IETagService eTagService = Substitute.For<IETagService>();
        _ = eTagService.GetCurrentETagAsync("orders", "test-tenant", Arg.Any<CancellationToken>())
            .Returns(serverETag);

        QueriesController controller = CreateController(mediator, eTagService);

        // Act
        IActionResult actionResult = await controller.Submit(CreateTestRequest(), null, CancellationToken.None);

        // Assert
        _ = actionResult.ShouldBeOfType<OkObjectResult>();
        controller.Response.Headers.ETag.ToString().ShouldBe($"\"{serverETag}\"");
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
        _ = actionResult.ShouldBeOfType<OkObjectResult>();
        controller.Response.Headers.ETag.ToString().ShouldBeEmpty();
    }

    [Fact]
    public async Task Submit_MultipleETagsOneMatches_Returns304() {
        // Arrange — self-routing ETags in comma-separated list
        string matchingETag = GenerateTestETag();
        string otherETag1 = GenerateTestETag();
        string otherETag2 = GenerateTestETag();
        IMediator mediator = Substitute.For<IMediator>();
        IETagService eTagService = Substitute.For<IETagService>();
        _ = eTagService.GetCurrentETagAsync("orders", "test-tenant", Arg.Any<CancellationToken>())
            .Returns(matchingETag);

        QueriesController controller = CreateController(mediator, eTagService);

        // Act
        IActionResult actionResult = await controller.Submit(
            CreateTestRequest(), $"\"{otherETag1}\", \"{matchingETag}\", \"{otherETag2}\"", CancellationToken.None);

        // Assert
        StatusCodeResult statusResult = actionResult.ShouldBeOfType<StatusCodeResult>();
        statusResult.StatusCode.ShouldBe(304);
    }

    [Fact]
    public async Task Submit_MultiValueHeaderWithMixedProjectionTypes_SkipsGate1Returns200() {
        // Arrange — mixed projections in a multi-value header must fail open to avoid false 304s
        string ordersETag = GenerateTestETag("orders");
        string countersETag = GenerateTestETag("counter");
        string responseETag = GenerateTestETag("orders");
        JsonElement resultPayload = JsonDocument.Parse("{\"data\":1}").RootElement;
        IMediator mediator = Substitute.For<IMediator>();
        _ = mediator.Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(new SubmitQueryResult("corr-1", resultPayload));
        IETagService eTagService = Substitute.For<IETagService>();
        _ = eTagService.GetCurrentETagAsync("orders", "test-tenant", Arg.Any<CancellationToken>())
            .Returns(responseETag);

        QueriesController controller = CreateController(mediator, eTagService);

        // Act
        IActionResult actionResult = await controller.Submit(
            CreateTestRequest(),
            $"\"{ordersETag}\", \"{countersETag}\"",
            CancellationToken.None);

        // Assert
        _ = actionResult.ShouldBeOfType<OkObjectResult>();
        controller.Response.Headers.ETag.ToString().ShouldBe($"\"{responseETag}\"");
        _ = await eTagService.Received(1).GetCurrentETagAsync("orders", "test-tenant", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Submit_MultipleETagsNoneMatch_Returns200() {
        // Arrange
        string serverETag = GenerateTestETag();
        JsonElement resultPayload = JsonDocument.Parse("{}").RootElement;
        IMediator mediator = Substitute.For<IMediator>();
        _ = mediator.Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(new SubmitQueryResult("corr-1", resultPayload));
        IETagService eTagService = Substitute.For<IETagService>();
        _ = eTagService.GetCurrentETagAsync("orders", "test-tenant", Arg.Any<CancellationToken>())
            .Returns(serverETag);

        QueriesController controller = CreateController(mediator, eTagService);
        string etagA = GenerateTestETag();
        string etagB = GenerateTestETag();

        // Act
        IActionResult actionResult = await controller.Submit(
            CreateTestRequest(), $"\"{etagA}\", \"{etagB}\"", CancellationToken.None);

        // Assert
        _ = actionResult.ShouldBeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Submit_WildcardIfNoneMatch_SkipsGate1AndReturns200() {
        // Arrange — AC #3: wildcard skips Gate 1 entirely (no decode, no actor call)
        JsonElement resultPayload = JsonDocument.Parse("{}").RootElement;
        IMediator mediator = Substitute.For<IMediator>();
        _ = mediator.Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(new SubmitQueryResult("corr-1", resultPayload));
        IETagService eTagService = Substitute.For<IETagService>();
        _ = eTagService.GetCurrentETagAsync("orders", "test-tenant", Arg.Any<CancellationToken>())
            .Returns("some-etag");

        QueriesController controller = CreateController(mediator, eTagService);

        // Act
        IActionResult actionResult = await controller.Submit(CreateTestRequest(), "*", CancellationToken.None);

        // Assert — wildcard skips Gate 1, proceeds to query, returns 200
        _ = actionResult.ShouldBeOfType<OkObjectResult>();
        _ = await mediator.Received(1).Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Submit_ETagServiceReturnsNull_ColdStartProceeds200() {
        // Arrange — ETag service returns null (cold start or failure)
        string selfRoutingETag = GenerateTestETag();
        JsonElement resultPayload = JsonDocument.Parse("{}").RootElement;
        IMediator mediator = Substitute.For<IMediator>();
        _ = mediator.Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(new SubmitQueryResult("corr-1", resultPayload));
        IETagService eTagService = Substitute.For<IETagService>();
        _ = eTagService.GetCurrentETagAsync("orders", "test-tenant", Arg.Any<CancellationToken>())
            .Returns((string?)null);

        QueriesController controller = CreateController(mediator, eTagService);

        // Act — even with If-None-Match, null ETag means no 304
        IActionResult actionResult = await controller.Submit(CreateTestRequest(), $"\"{selfRoutingETag}\"", CancellationToken.None);

        // Assert
        _ = actionResult.ShouldBeOfType<OkObjectResult>();
        _ = await mediator.Received(1).Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Submit_ETagServiceThrows_FailsOpenAndReturns200() {
        // Arrange — service throws on both Gate 1 decode path and 200 response path
        string selfRoutingETag = GenerateTestETag();
        JsonElement resultPayload = JsonDocument.Parse("{}").RootElement;
        IMediator mediator = Substitute.For<IMediator>();
        _ = mediator.Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(new SubmitQueryResult("corr-1", resultPayload));
        IETagService eTagService = Substitute.For<IETagService>();
        _ = eTagService.GetCurrentETagAsync("orders", "test-tenant", Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("boom"));

        QueriesController controller = CreateController(mediator, eTagService);

        // Act
        IActionResult actionResult = await controller.Submit(CreateTestRequest(), $"\"{selfRoutingETag}\"", CancellationToken.None);

        // Assert
        _ = actionResult.ShouldBeOfType<OkObjectResult>();
        controller.Response.Headers.ETag.ToString().ShouldBeEmpty();
        _ = await mediator.Received(1).Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Submit_ETagHeaderFormat_DoubleQuoted() {
        // Arrange
        string serverETag = GenerateTestETag();
        JsonElement resultPayload = JsonDocument.Parse("{}").RootElement;
        IMediator mediator = Substitute.For<IMediator>();
        _ = mediator.Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(new SubmitQueryResult("corr-1", resultPayload));
        IETagService eTagService = Substitute.For<IETagService>();
        _ = eTagService.GetCurrentETagAsync("orders", "test-tenant", Arg.Any<CancellationToken>())
            .Returns(serverETag);

        QueriesController controller = CreateController(mediator, eTagService);

        // Act
        _ = await controller.Submit(CreateTestRequest(), null, CancellationToken.None);

        // Assert — RFC 7232: ETag header must be double-quoted
        string eTagHeader = controller.Response.Headers.ETag.ToString();
        eTagHeader.ShouldStartWith("\"");
        eTagHeader.ShouldEndWith("\"");
        eTagHeader.ShouldBe($"\"{serverETag}\"");
    }

    [Fact]
    public async Task Submit_MoreThan10ETagValues_SkipsGate1Returns200() {
        // Arrange — 11 self-routing ETags, one matching — ETagMatches skips after >10 values
        string serverETag = GenerateTestETag();
        JsonElement resultPayload = JsonDocument.Parse("{}").RootElement;
        IMediator mediator = Substitute.For<IMediator>();
        _ = mediator.Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(new SubmitQueryResult("corr-1", resultPayload));
        IETagService eTagService = Substitute.For<IETagService>();
        _ = eTagService.GetCurrentETagAsync("orders", "test-tenant", Arg.Any<CancellationToken>())
            .Returns(serverETag);

        QueriesController controller = CreateController(mediator, eTagService);

        // Generate 10 random ETags and insert the server ETag at position 5
        var etags = Enumerable.Range(1, 11).Select(_ => GenerateTestETag()).ToList();
        etags[4] = serverETag;
        string manyETags = string.Join(", ", etags.Select(e => $"\"{e}\""));

        // Act
        IActionResult actionResult = await controller.Submit(CreateTestRequest(), manyETags, CancellationToken.None);

        // Assert — should proceed to full query despite containing a match (>10 limit)
        _ = actionResult.ShouldBeOfType<OkObjectResult>();
    }

    // ===== Backward compatibility and self-routing decode tests =====

    [Fact]
    public async Task Submit_OldFormatETag_CacheMissAndReturnsNewFormatETag() {
        // Arrange — old-format ETag (plain GUID, no dot) → decode fails → cache miss
        string oldFormatETag = Convert.ToBase64String(Guid.NewGuid().ToByteArray())
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
        string serverETag = GenerateTestETag();
        JsonElement resultPayload = JsonDocument.Parse("{}").RootElement;
        IMediator mediator = Substitute.For<IMediator>();
        _ = mediator.Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(new SubmitQueryResult("corr-1", resultPayload));
        IETagService eTagService = Substitute.For<IETagService>();
        _ = eTagService.GetCurrentETagAsync("orders", "test-tenant", Arg.Any<CancellationToken>())
            .Returns(serverETag);

        QueriesController controller = CreateController(mediator, eTagService);

        // Act
        IActionResult actionResult = await controller.Submit(CreateTestRequest(), $"\"{oldFormatETag}\"", CancellationToken.None);

        // Assert — old format → cache miss → 200, response has new self-routing ETag
        _ = actionResult.ShouldBeOfType<OkObjectResult>();
        string responseETag = controller.Response.Headers.ETag.ToString();
        responseETag.ShouldContain(".");  // New self-routing format
    }

    [Fact]
    public async Task Submit_WildcardETag_NoDecodeAttemptProceedsToGate2() {
        // Arrange — AC #3: wildcard contains no projection type to decode
        JsonElement resultPayload = JsonDocument.Parse("{}").RootElement;
        IMediator mediator = Substitute.For<IMediator>();
        _ = mediator.Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(new SubmitQueryResult("corr-1", resultPayload));
        IETagService eTagService = Substitute.For<IETagService>();

        QueriesController controller = CreateController(mediator, eTagService);

        // Act
        IActionResult actionResult = await controller.Submit(CreateTestRequest(), "*", CancellationToken.None);

        // Assert — wildcard skips Gate 1 entirely, no ETag service call for decode
        _ = actionResult.ShouldBeOfType<OkObjectResult>();
    }

    [Fact]
    public void AnalyzeHeaderProjectionTypes_WeakETagPrefix_FailsDecodeReturnsNull() {
        // Arrange — W/"etag" per RFC 7232 §2.3; our ETags are strong validators
        string validETag = GenerateTestETag("orders");

        // Control case: a strong validator decodes as expected.
        HeaderProjectionTypeAnalysis strongResult = AnalyzeHeader($"\"{validETag}\"");
        strongResult.ProjectionType.ShouldBe("orders");
        strongResult.HasMixedProjectionTypes.ShouldBeFalse();

        // W/ prefix outside quotes: the quote stripping only removes outer quotes,
        // leaving "W/" prefix on the value → TryDecode fails → null projection type
        HeaderProjectionTypeAnalysis result = AnalyzeHeader($"W/\"{validETag}\"");

        result.ProjectionType.ShouldBeNull();
        result.HasMixedProjectionTypes.ShouldBeFalse();
    }

    [Fact]
    public async Task Submit_NonAsciiProjectionTypeInIfNoneMatch_RoutesUsingDecodedProjectionType() {
        // Arrange — routing should follow decoded projection type, not request.Domain
        string projectionType = "données";
        string nonAsciiETag = GenerateTestETag(projectionType);
        IMediator mediator = Substitute.For<IMediator>();
        IETagService eTagService = Substitute.For<IETagService>();
        _ = eTagService.GetCurrentETagAsync(projectionType, "test-tenant", Arg.Any<CancellationToken>())
            .Returns(nonAsciiETag);

        QueriesController controller = CreateController(mediator, eTagService);

        // Act
        IActionResult actionResult = await controller.Submit(
            CreateTestRequest(), $"\"{nonAsciiETag}\"", CancellationToken.None);

        // Assert — Gate 1 uses decoded non-ASCII projection and returns 304 on match
        StatusCodeResult statusResult = actionResult.ShouldBeOfType<StatusCodeResult>();
        statusResult.StatusCode.ShouldBe(304);
        _ = await eTagService.Received(1).GetCurrentETagAsync(projectionType, "test-tenant", Arg.Any<CancellationToken>());
        _ = await mediator.DidNotReceive().Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Submit_WeakETagPrefix_CacheMissReturns200() {
        // Arrange — W/ prefix causes decode failure → cache miss → full query
        string serverETag = GenerateTestETag();
        JsonElement resultPayload = JsonDocument.Parse("{\"data\":1}").RootElement;
        IMediator mediator = Substitute.For<IMediator>();
        _ = mediator.Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(new SubmitQueryResult("corr-1", resultPayload));
        IETagService eTagService = Substitute.For<IETagService>();
        _ = eTagService.GetCurrentETagAsync("orders", "test-tenant", Arg.Any<CancellationToken>())
            .Returns(serverETag);

        QueriesController controller = CreateController(mediator, eTagService);

        // Act — W/ prefix on If-None-Match
        IActionResult actionResult = await controller.Submit(
            CreateTestRequest(), $"W/\"{serverETag}\"", CancellationToken.None);

        // Assert — W/ causes decode failure → cache miss → 200 with new ETag
        _ = actionResult.ShouldBeOfType<OkObjectResult>();
        controller.Response.Headers.ETag.ToString().ShouldBe($"\"{serverETag}\"");
        // Verify no W/ prefix in response ETag
        controller.Response.Headers.ETag.ToString().ShouldNotStartWith("W/");
        _ = await mediator.Received(1).Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>());
        _ = await eTagService.Received(1).GetCurrentETagAsync("orders", "test-tenant", Arg.Any<CancellationToken>());
    }

    [Fact]
    public void AnalyzeHeaderProjectionTypes_MixedProjectionTypes_ReturnsSkipSignal() {
        string ordersETag = GenerateTestETag("orders");
        string countersETag = GenerateTestETag("counter");

        HeaderProjectionTypeAnalysis result = AnalyzeHeader($"\"{ordersETag}\", \"{countersETag}\"");

        result.HasMixedProjectionTypes.ShouldBeTrue();
        result.ProjectionType.ShouldBeNull();
    }

    [Fact]
    public void AnalyzeHeaderProjectionTypes_SameProjectionTypes_ReturnsProjectionType() {
        string etag1 = GenerateTestETag("orders");
        string etag2 = GenerateTestETag("orders");

        HeaderProjectionTypeAnalysis result = AnalyzeHeader($"\"{etag1}\", \"{etag2}\"");

        result.HasMixedProjectionTypes.ShouldBeFalse();
        result.ProjectionType.ShouldBe("orders");
    }

    [Fact]
    public async Task Submit_ETagPreCheckPerformance_P99UnderFiveMilliseconds() {
        // Arrange — in-memory warm actor equivalent: matching self-routing ETag with constant service response
        string testETag = GenerateTestETag();
        IMediator mediator = Substitute.For<IMediator>();
        IETagService eTagService = new ConstantETagService(testETag);
        QueriesController controller = CreateController(mediator, eTagService);
        SubmitQueryRequest request = CreateTestRequest();
        string header = $"\"{testETag}\"";

        // Warm-up
        for (int i = 0; i < 25; i++) {
            _ = await controller.Submit(request, header, CancellationToken.None);
        }

        List<double> durationsMs = [];

        // Act
        for (int i = 0; i < 200; i++) {
            long started = Stopwatch.GetTimestamp();
            IActionResult result = await controller.Submit(request, header, CancellationToken.None);
            result.ShouldBeOfType<StatusCodeResult>().StatusCode.ShouldBe(304);
            durationsMs.Add(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        }

        // Assert
        double p99 = durationsMs.OrderBy(x => x).ElementAt((int)Math.Ceiling(durationsMs.Count * 0.99) - 1);
        p99.ShouldBeLessThan(5d);
    }

    // ===== Story 18-8: ProjectionType passthrough to ETag response =====

    [Fact]
    public async Task Submit_WithProjectionType_ETagFetchUsesProjectionType() {
        // Arrange — SubmitQueryResult has ProjectionType="order-list"
        string orderListETag = GenerateTestETag("order-list");
        JsonElement resultPayload = JsonDocument.Parse("{\"data\":1}").RootElement;
        IMediator mediator = Substitute.For<IMediator>();
        _ = mediator.Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(new SubmitQueryResult("corr-1", resultPayload, "order-list"));
        IETagService eTagService = Substitute.For<IETagService>();
        _ = eTagService.GetCurrentETagAsync("order-list", "test-tenant", Arg.Any<CancellationToken>())
            .Returns(orderListETag);

        QueriesController controller = CreateController(mediator, eTagService);

        // Act
        IActionResult actionResult = await controller.Submit(CreateTestRequest(), null, CancellationToken.None);

        // Assert — ETag fetched using "order-list", not "orders" (request.Domain)
        _ = actionResult.ShouldBeOfType<OkObjectResult>();
        controller.Response.Headers.ETag.ToString().ShouldBe($"\"{orderListETag}\"");
        _ = await eTagService.Received(1).GetCurrentETagAsync("order-list", "test-tenant", Arg.Any<CancellationToken>());
        _ = await eTagService.DidNotReceive().GetCurrentETagAsync("orders", "test-tenant", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Submit_NullProjectionType_ETagFetchUsesRequestDomain() {
        // Arrange — SubmitQueryResult has ProjectionType=null → fallback to request.Domain
        string ordersETag = GenerateTestETag();
        JsonElement resultPayload = JsonDocument.Parse("{\"data\":1}").RootElement;
        IMediator mediator = Substitute.For<IMediator>();
        _ = mediator.Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(new SubmitQueryResult("corr-1", resultPayload, null));
        IETagService eTagService = Substitute.For<IETagService>();
        _ = eTagService.GetCurrentETagAsync("orders", "test-tenant", Arg.Any<CancellationToken>())
            .Returns(ordersETag);

        QueriesController controller = CreateController(mediator, eTagService);

        // Act
        IActionResult actionResult = await controller.Submit(CreateTestRequest(), null, CancellationToken.None);

        // Assert — fallback to "orders" (request.Domain)
        _ = actionResult.ShouldBeOfType<OkObjectResult>();
        controller.Response.Headers.ETag.ToString().ShouldBe($"\"{ordersETag}\"");
        _ = await eTagService.Received(1).GetCurrentETagAsync("orders", "test-tenant", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Submit_EmptyProjectionType_ETagFetchUsesRequestDomain() {
        // Arrange — empty ProjectionType should fall back to request.Domain
        string ordersETag = GenerateTestETag();
        JsonElement resultPayload = JsonDocument.Parse("{\"data\":1}").RootElement;
        IMediator mediator = Substitute.For<IMediator>();
        _ = mediator.Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(new SubmitQueryResult("corr-1", resultPayload, string.Empty));
        IETagService eTagService = Substitute.For<IETagService>();
        _ = eTagService.GetCurrentETagAsync("orders", "test-tenant", Arg.Any<CancellationToken>())
            .Returns(ordersETag);

        QueriesController controller = CreateController(mediator, eTagService);

        // Act
        IActionResult actionResult = await controller.Submit(CreateTestRequest(), null, CancellationToken.None);

        // Assert
        _ = actionResult.ShouldBeOfType<OkObjectResult>();
        controller.Response.Headers.ETag.ToString().ShouldBe($"\"{ordersETag}\"");
        _ = await eTagService.Received(1).GetCurrentETagAsync("orders", "test-tenant", Arg.Any<CancellationToken>());
    }

    private sealed class ConstantETagService(string etag) : IETagService {
        public Task<string?> GetCurrentETagAsync(string projectionType, string tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(etag);
    }
}
