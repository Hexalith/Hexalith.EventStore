using System.Reflection;
using System.Text.Json;

using Hexalith.Commons.UniqueIds;
using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Sample.Api.Services;
using Hexalith.EventStore.Sample.Counter.Commands;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

using Shouldly;

namespace Hexalith.EventStore.Sample.Tests.SampleApi;

public sealed class SampleApiGeneratedControllerRuntimeTests
{
    [Fact]
    public async Task CounterQuery_WhenGatewayReturnsSuccess_ForwardsPayloadAndMetadataHeaders()
    {
        GeneratedController controller = CreateController();
        DateTimeOffset servedAt = new(2026, 7, 7, 10, 30, 0, TimeSpan.Zero);
        controller.Gateway.QueryHandler = static (_, _, _) =>
        {
            JsonElement payload = JsonSerializer.SerializeToElement(new
            {
                counterId = "counter-1",
                value = 7,
            });

            return Task.FromResult(new EventStoreQueryResult(
                "01KTESTQUERY200000000000",
                payload,
                IsNotModified: false,
                ETag: "counter-version")
            {
                Metadata = new QueryResponseMetadata(
                    IsStale: false,
                    IsDegraded: true,
                    ProjectionVersion: "42",
                    ServedAt: new DateTimeOffset(2026, 7, 7, 10, 30, 0, TimeSpan.Zero),
                    Paging: new QueryPagingMetadata(
                        PageSize: 25,
                        Offset: 50,
                        NextCursor: "opaque-next",
                        TotalCount: 125,
                        HasMore: true),
                    WarningCodes:
                    [
                        QueryWarningCodes.DegradedSearch,
                        QueryWarningCodes.ETagUnavailable,
                    ]),
            });
        };

        IActionResult result = await InvokeQueryAsync(controller);

        OkObjectResult ok = result.ShouldBeOfType<OkObjectResult>();
        JsonElement payload = ok.Value.ShouldBeOfType<JsonElement>();
        payload.GetProperty("counterId").GetString().ShouldBe("counter-1");
        payload.GetProperty("value").GetInt32().ShouldBe(7);

        controller.Gateway.QueryCallCount.ShouldBe(1);
        SubmitQueryRequest request = controller.Gateway.LastQueryRequest.ShouldNotBeNull();
        request.Tenant.ShouldBe("tenant-a");
        request.Domain.ShouldBe("counter");
        request.AggregateId.ShouldBe("counter-1");
        request.EntityId.ShouldBe("counter-1");
        request.QueryType.ShouldBe("get-counter-status");
        request.ProjectionType.ShouldBe("counter");
        controller.Gateway.LastIfNoneMatch.ShouldBeNull();

        IHeaderDictionary headers = controller.Controller.HttpContext.Response.Headers;
        headers[HeaderNames.ETag].ToString().ShouldBe("\"counter-version\"");
        headers["X-Hexalith-Projection-Version"].ToString().ShouldBe("42");
        headers["X-Hexalith-Served-At"].ToString().ShouldBe(servedAt.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
        headers["X-Hexalith-Is-Stale"].ToString().ShouldBe("false");
        headers["X-Hexalith-Is-Degraded"].ToString().ShouldBe("true");
        headers["X-Hexalith-Warning-Codes"].ToString().ShouldBe("degraded_search,etag_unavailable");
        headers["X-Hexalith-Page-Size"].ToString().ShouldBe("25");
        headers["X-Hexalith-Page-Offset"].ToString().ShouldBe("50");
        headers["X-Hexalith-Page-Total-Count"].ToString().ShouldBe("125");
        headers["X-Hexalith-Page-Has-More"].ToString().ShouldBe("true");
        headers["X-Hexalith-Next-Cursor"].ToString().ShouldBe("opaque-next");
    }

    [Fact]
    public async Task CounterQuery_WhenGatewayReturnsNotModified_ForwardsIfNoneMatchAndStrongETag()
    {
        GeneratedController controller = CreateController();
        controller.Gateway.QueryHandler = static (_, _, _) =>
            Task.FromResult(new EventStoreQueryResult(
                CorrelationId: null,
                Payload: null,
                IsNotModified: true,
                ETag: "counter-version")
            {
                Metadata = new QueryResponseMetadata(ETag: "counter-version", IsNotModified: true),
            });

        IActionResult result = await InvokeQueryAsync(controller, "\"counter-version\"");

        StatusCodeResult notModified = result.ShouldBeOfType<StatusCodeResult>();
        notModified.StatusCode.ShouldBe(StatusCodes.Status304NotModified);
        controller.Gateway.QueryCallCount.ShouldBe(1);
        controller.Gateway.LastIfNoneMatch.ShouldBe("\"counter-version\"");
        controller.Controller.HttpContext.Response.Headers[HeaderNames.ETag].ToString().ShouldBe("\"counter-version\"");
    }

    [Fact]
    public async Task IncrementCounter_WhenBodyMatchesRoute_SubmitsGatewayCommandAndReturnsAccepted()
    {
        GeneratedController controller = CreateController();
        controller.Gateway.CommandHandler = static (_, _) =>
            Task.FromResult(new SubmitCommandResponse("01KTESTCOMMANDSTATUS000000"));

        IActionResult result = await InvokeIncrementCounterAsync(
            controller,
            "counter-1",
            new IncrementCounter("counter-1"));

        AcceptedResult accepted = result.ShouldBeOfType<AcceptedResult>();
        SubmitCommandResponse response = accepted.Value.ShouldBeOfType<SubmitCommandResponse>();
        response.CorrelationId.ShouldBe("01KTESTCOMMANDSTATUS000000");

        controller.Gateway.CommandCallCount.ShouldBe(1);
        SubmitCommandRequest request = controller.Gateway.LastCommandRequest.ShouldNotBeNull();
        request.MessageId.ShouldNotBeNullOrWhiteSpace();
        _ = UniqueIdHelper.ExtractTimestamp(request.MessageId);
        request.Tenant.ShouldBe("tenant-a");
        request.Domain.ShouldBe("counter");
        request.AggregateId.ShouldBe("counter-1");
        request.CommandType.ShouldBe("increment-counter");
        request.Payload.GetProperty("counterId").GetString().ShouldBe("counter-1");

        IHeaderDictionary headers = controller.Controller.HttpContext.Response.Headers;
        headers[HeaderNames.RetryAfter].ToString().ShouldBe("1");
        headers[HeaderNames.Location].ToString().ShouldBe("/api/v1/commands/status/01KTESTCOMMANDSTATUS000000");
    }

    [Fact]
    public async Task IncrementCounter_WhenBodyIsNull_ReturnsBadRequestBeforeGatewayCall()
    {
        GeneratedController controller = CreateController();

        IActionResult result = await InvokeIncrementCounterAsync(controller, "counter-1", body: null);

        ObjectResult badRequest = result.ShouldBeOfType<ObjectResult>();
        badRequest.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        badRequest.ContentTypes.ShouldContain("application/problem+json");
        ProblemDetails problem = badRequest.Value.ShouldBeOfType<ProblemDetails>();
        problem.Detail.ShouldBe("Request body is required.");
        controller.Gateway.CommandCallCount.ShouldBe(0);
    }

    [Fact]
    public async Task IncrementCounter_WhenRouteAndBodyAggregateMismatch_ReturnsBadRequestBeforeGatewayCall()
    {
        GeneratedController controller = CreateController();

        IActionResult result = await InvokeIncrementCounterAsync(
            controller,
            "counter-1",
            new IncrementCounter("counter-2"));

        ObjectResult badRequest = result.ShouldBeOfType<ObjectResult>();
        badRequest.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        badRequest.ContentTypes.ShouldContain("application/problem+json");
        ProblemDetails problem = badRequest.Value.ShouldBeOfType<ProblemDetails>();
        problem.Detail.ShouldContain("does not match");
        controller.Gateway.CommandCallCount.ShouldBe(0);
    }

    [Fact]
    public async Task CounterQuery_WhenGatewayThrowsForbidden_ReturnsSafeProblemDetails()
    {
        GeneratedController controller = CreateController();
        controller.Gateway.QueryHandler = static (_, _, _) =>
            throw new EventStoreGatewayException(
                StatusCodes.Status403Forbidden,
                "Forbidden",
                detail: "Access denied.",
                correlationId: "01KTESTCORRELATION00000",
                tenantId: "tenant-a",
                reasonCode: "tenant-forbidden");

        IActionResult result = await InvokeQueryAsync(controller);

        ObjectResult forbidden = result.ShouldBeOfType<ObjectResult>();
        forbidden.StatusCode.ShouldBe(StatusCodes.Status403Forbidden);
        forbidden.ContentTypes.ShouldContain("application/problem+json");
        ProblemDetails problem = forbidden.Value.ShouldBeOfType<ProblemDetails>();
        problem.Status.ShouldBe(StatusCodes.Status403Forbidden);
        problem.Title.ShouldBe("Forbidden");
        problem.Extensions["correlationId"].ShouldBe("01KTESTCORRELATION00000");
        problem.Extensions["tenantId"].ShouldBe("tenant-a");
        problem.Extensions["reasonCode"].ShouldBe("tenant-forbidden");
        controller.Gateway.QueryCallCount.ShouldBe(1);
    }

    private static GeneratedController CreateController()
    {
        Type controllerType = typeof(DaprAppIdHandler).Assembly.GetType(
            "Hexalith.EventStore.Sample.Api.Generated.CounterRestController",
            throwOnError: true)!;
        var gateway = new FakeEventStoreGatewayClient();
        var controller = (ControllerBase)Activator.CreateInstance(controllerType, gateway)!;
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext(),
        };

        return new GeneratedController(controller, gateway);
    }

    private static async Task<IActionResult> InvokeIncrementCounterAsync(
        GeneratedController controller,
        string counterId,
        IncrementCounter? body)
    {
        MethodInfo method = controller.Controller.GetType().GetMethod("IncrementCounterCommandAsync")
            ?? throw new MissingMethodException(controller.Controller.GetType().FullName, "IncrementCounterCommandAsync");
        var task = (Task<IActionResult>)method.Invoke(
            controller.Controller,
            ["tenant-a", counterId, body, TestContext.Current.CancellationToken])!;
        return await task.ConfigureAwait(false);
    }

    private static async Task<IActionResult> InvokeQueryAsync(
        GeneratedController controller,
        string? ifNoneMatch = null)
    {
        MethodInfo method = controller.Controller.GetType().GetMethod("GetCounterStatusQueryQueryAsync")
            ?? throw new MissingMethodException(controller.Controller.GetType().FullName, "GetCounterStatusQueryQueryAsync");
        var task = (Task<IActionResult>)method.Invoke(
            controller.Controller,
            ["tenant-a", "counter-1", ifNoneMatch, TestContext.Current.CancellationToken])!;
        return await task.ConfigureAwait(false);
    }

    private sealed record GeneratedController(ControllerBase Controller, FakeEventStoreGatewayClient Gateway);
}
