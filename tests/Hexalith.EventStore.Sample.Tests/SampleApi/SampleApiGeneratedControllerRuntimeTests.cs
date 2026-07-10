extern alias SampleApi;

using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json;

using Hexalith.Commons.UniqueIds;
using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Sample.Counter.Commands;
using SampleApiAssemblyMarker = SampleApi::Hexalith.EventStore.Sample.Api.Services.InboundBearerForwardingHandler;

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
        controller.Gateway.LastQueryCancellationToken.ShouldBe(TestContext.Current.CancellationToken);

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
    public async Task CounterQuery_WhenGatewayReturnsSuccessWithoutMetadata_OmitsFreshnessAndPagingHeaders()
    {
        GeneratedController controller = CreateController();
        controller.Gateway.QueryHandler = static (_, _, _) =>
        {
            JsonElement payload = JsonSerializer.SerializeToElement(new
            {
                counterId = "counter-1",
            });

            return Task.FromResult(new EventStoreQueryResult(
                "01KTESTQUERY200000000000",
                payload,
                IsNotModified: false,
                ETag: null));
        };

        IActionResult result = await InvokeQueryAsync(controller);

        _ = result.ShouldBeOfType<OkObjectResult>();
        AssertMetadataHeadersAbsent(controller.Controller.HttpContext.Response.Headers);
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
    public async Task CounterQuery_WhenGatewayReturnsNotModifiedWithoutStrongETag_ReturnsBadGatewayProblem()
        => await AssertNotModifiedWithoutStrongETagAsync("W/\"counter-version\"");

    [Fact]
    public async Task CounterQuery_WhenGatewayReturnsNotModifiedWithoutETag_ReturnsBadGatewayProblem()
        => await AssertNotModifiedWithoutStrongETagAsync(null);

    [Fact]
    public async Task IncrementCounter_WhenBodyMatchesRoute_SubmitsGatewayCommandAndReturnsAccepted()
    {
        GeneratedController controller = CreateController(
            new FakeCommandStatusLocationBuilder("https://gateway.example"));
        string statusId = UniqueIdHelper.GenerateSortableUniqueStringId();
        controller.Gateway.CommandHandler = (_, _) =>
            Task.FromResult(new SubmitCommandResponse(statusId));

        IActionResult result = await InvokeIncrementCounterAsync(
            controller,
            "counter-1",
            new IncrementCounter("counter-1"));

        AcceptedResult accepted = result.ShouldBeOfType<AcceptedResult>();
        SubmitCommandResponse response = accepted.Value.ShouldBeOfType<SubmitCommandResponse>();
        response.CorrelationId.ShouldBe(statusId);
        _ = UniqueIdHelper.ExtractTimestamp(response.CorrelationId);

        controller.Gateway.CommandCallCount.ShouldBe(1);
        SubmitCommandRequest request = controller.Gateway.LastCommandRequest.ShouldNotBeNull();
        request.MessageId.ShouldNotBeNullOrWhiteSpace();
        _ = UniqueIdHelper.ExtractTimestamp(request.MessageId);
        request.Tenant.ShouldBe("tenant-a");
        request.Domain.ShouldBe("counter");
        request.AggregateId.ShouldBe("counter-1");
        request.CommandType.ShouldBe("increment-counter");
        request.Payload.GetProperty("counterId").GetString().ShouldBe("counter-1");
        controller.Gateway.LastCommandCancellationToken.ShouldBe(TestContext.Current.CancellationToken);

        IHeaderDictionary headers = controller.Controller.HttpContext.Response.Headers;
        headers[HeaderNames.RetryAfter].ToString().ShouldBe("1");
        headers[HeaderNames.Location].ToString().ShouldBe($"https://gateway.example/api/v1/commands/status/{statusId}");
        headers[HeaderNames.Location].ToString().ShouldNotStartWith("/");
    }

    [Fact]
    public async Task IncrementCounter_WhenStatusBaseUnconfigured_SubmitsGatewayCommandAndOmitsLocation()
    {
        GeneratedController controller = CreateController();
        string statusId = UniqueIdHelper.GenerateSortableUniqueStringId();
        controller.Gateway.CommandHandler = (_, _) =>
            Task.FromResult(new SubmitCommandResponse(statusId));

        IActionResult result = await InvokeIncrementCounterAsync(
            controller,
            "counter-1",
            new IncrementCounter("counter-1"));

        AcceptedResult accepted = result.ShouldBeOfType<AcceptedResult>();
        SubmitCommandResponse response = accepted.Value.ShouldBeOfType<SubmitCommandResponse>();
        response.CorrelationId.ShouldBe(statusId);
        controller.Gateway.CommandCallCount.ShouldBe(1);

        IHeaderDictionary headers = controller.Controller.HttpContext.Response.Headers;
        headers[HeaderNames.RetryAfter].ToString().ShouldBe("1");
        headers.ContainsKey(HeaderNames.Location).ShouldBeFalse();
    }

    [Fact]
    public async Task CounterCommands_WhenBodiesMatchRoute_SubmitExpectedGatewayCommands()
    {
        foreach (CommandCase commandCase in CommandCases())
        {
            GeneratedController controller = CreateController(
                new FakeCommandStatusLocationBuilder("https://gateway.example"));
            string statusId = UniqueIdHelper.GenerateSortableUniqueStringId();
            controller.Gateway.CommandHandler = (_, _) =>
                Task.FromResult(new SubmitCommandResponse(statusId));

            IActionResult result = await InvokeCommandAsync(
                controller,
                commandCase.MethodName,
                "counter-1",
                commandCase.MatchingBody);

            AcceptedResult accepted = result.ShouldBeOfType<AcceptedResult>();
            SubmitCommandResponse response = accepted.Value.ShouldBeOfType<SubmitCommandResponse>();
            response.CorrelationId.ShouldBe(statusId);
            _ = UniqueIdHelper.ExtractTimestamp(response.CorrelationId);

            controller.Gateway.CommandCallCount.ShouldBe(1);
            SubmitCommandRequest request = controller.Gateway.LastCommandRequest.ShouldNotBeNull();
            request.MessageId.ShouldNotBeNullOrWhiteSpace();
            _ = UniqueIdHelper.ExtractTimestamp(request.MessageId);
            request.Tenant.ShouldBe("tenant-a");
            request.Domain.ShouldBe("counter");
            request.AggregateId.ShouldBe("counter-1");
            request.CommandType.ShouldBe(commandCase.CommandType);
            request.Payload.GetProperty("counterId").GetString().ShouldBe("counter-1");
            controller.Gateway.LastCommandCancellationToken.ShouldBe(TestContext.Current.CancellationToken);
            controller.Controller.HttpContext.Response.Headers[HeaderNames.RetryAfter].ToString().ShouldBe("1");
            controller.Controller.HttpContext.Response.Headers[HeaderNames.Location].ToString()
                .ShouldBe($"https://gateway.example/api/v1/commands/status/{statusId}");
            controller.Controller.HttpContext.Response.Headers[HeaderNames.Location].ToString()
                .ShouldNotStartWith("/");
        }
    }

    [Fact]
    public async Task CounterCommands_WhenBodyIsNull_ReturnBadRequestBeforeGatewayCall()
    {
        foreach (CommandCase commandCase in CommandCases())
        {
            GeneratedController controller = CreateController();

            IActionResult result = await InvokeCommandAsync(
                controller,
                commandCase.MethodName,
                "counter-1",
                body: null);

            ObjectResult badRequest = result.ShouldBeOfType<ObjectResult>();
            badRequest.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
            badRequest.ContentTypes.ShouldContain("application/problem+json");
            ProblemDetails problem = badRequest.Value.ShouldBeOfType<ProblemDetails>();
            problem.Detail.ShouldBe("Request body is required.");
            controller.Gateway.CommandCallCount.ShouldBe(0);
        }
    }

    [Fact]
    public async Task CounterCommands_WhenRouteAndBodyAggregateMismatch_ReturnBadRequestBeforeGatewayCall()
    {
        foreach (CommandCase commandCase in CommandCases())
        {
            GeneratedController controller = CreateController();

            IActionResult result = await InvokeCommandAsync(
                controller,
                commandCase.MethodName,
                "counter-1",
                commandCase.MismatchedBody);

            ObjectResult badRequest = result.ShouldBeOfType<ObjectResult>();
            badRequest.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
            badRequest.ContentTypes.ShouldContain("application/problem+json");
            ProblemDetails problem = badRequest.Value.ShouldBeOfType<ProblemDetails>();
            problem.Detail.ShouldContain("does not match");
            controller.Gateway.CommandCallCount.ShouldBe(0);
        }
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

    [Fact]
    public async Task CounterCommands_WhenGatewayThrowsForbidden_ReturnSafeProblemDetails()
    {
        foreach (CommandCase commandCase in CommandCases())
        {
            GeneratedController controller = CreateController();
            controller.Gateway.CommandHandler = static (_, _) =>
                throw new EventStoreGatewayException(
                    StatusCodes.Status403Forbidden,
                    "Forbidden",
                    detail: "Access denied.",
                    correlationId: "01KTESTCORRELATION00000",
                    tenantId: "tenant-a",
                    reasonCode: "tenant-forbidden");

            IActionResult result = await InvokeCommandAsync(
                controller,
                commandCase.MethodName,
                "counter-1",
                commandCase.MatchingBody);

            ObjectResult forbidden = result.ShouldBeOfType<ObjectResult>();
            forbidden.StatusCode.ShouldBe(StatusCodes.Status403Forbidden);
            forbidden.ContentTypes.ShouldContain("application/problem+json");
            ProblemDetails problem = forbidden.Value.ShouldBeOfType<ProblemDetails>();
            problem.Status.ShouldBe(StatusCodes.Status403Forbidden);
            problem.Title.ShouldBe("Forbidden");
            problem.Extensions["correlationId"].ShouldBe("01KTESTCORRELATION00000");
            problem.Extensions["tenantId"].ShouldBe("tenant-a");
            problem.Extensions["reasonCode"].ShouldBe("tenant-forbidden");
            controller.Gateway.CommandCallCount.ShouldBe(1);
        }
    }

    private static async Task AssertNotModifiedWithoutStrongETagAsync(string? etag)
    {
        GeneratedController controller = CreateController();
        controller.Gateway.QueryHandler = (_, _, _) =>
            Task.FromResult(new EventStoreQueryResult(
                CorrelationId: null,
                Payload: null,
                IsNotModified: true,
                ETag: etag)
            {
                Metadata = new QueryResponseMetadata(
                    IsStale: false,
                    ProjectionVersion: "42",
                    ServedAt: new DateTimeOffset(2026, 7, 7, 10, 30, 0, TimeSpan.Zero)),
            });

        IActionResult result = await InvokeQueryAsync(controller, "\"counter-version\"").ConfigureAwait(false);

        ObjectResult badGateway = result.ShouldBeOfType<ObjectResult>();
        badGateway.StatusCode.ShouldBe(StatusCodes.Status502BadGateway);
        badGateway.ContentTypes.ShouldContain("application/problem+json");
        ProblemDetails problem = badGateway.Value.ShouldBeOfType<ProblemDetails>();
        problem.Detail.ShouldBe("Not-modified query response requires a strong ETag.");
        AssertMetadataHeadersAbsent(controller.Controller.HttpContext.Response.Headers);
    }

    private static CommandCase[] CommandCases()
        =>
        [
            new(
                "IncrementCounterCommandAsync",
                new IncrementCounter("counter-1"),
                new IncrementCounter("counter-2"),
                "increment-counter"),
            new(
                "DecrementCounterCommandAsync",
                new DecrementCounter("counter-1"),
                new DecrementCounter("counter-2"),
                "decrement-counter"),
            new(
                "ResetCounterCommandAsync",
                new ResetCounter("counter-1"),
                new ResetCounter("counter-2"),
                "reset-counter"),
            new(
                "CloseCounterCommandAsync",
                new CloseCounter("counter-1"),
                new CloseCounter("counter-2"),
                "close-counter"),
        ];

    private static GeneratedController CreateController(ICommandStatusLocationBuilder? statusLocationBuilder = null)
    {
        Type controllerType = typeof(SampleApiAssemblyMarker).Assembly.GetType(
            "Hexalith.EventStore.Sample.Api.Generated.CounterRestController",
            throwOnError: true)!;
        var gateway = new FakeEventStoreGatewayClient();
        var controller = (ControllerBase)Activator.CreateInstance(
            controllerType,
            gateway,
            statusLocationBuilder ?? new FakeCommandStatusLocationBuilder(gatewayStatusBase: null))!;
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
        => await InvokeCommandAsync(controller, "IncrementCounterCommandAsync", counterId, body).ConfigureAwait(false);

    private static async Task<IActionResult> InvokeCommandAsync(
        GeneratedController controller,
        string methodName,
        string counterId,
        object? body)
    {
        MethodInfo method = controller.Controller.GetType().GetMethod(methodName)
            ?? throw new MissingMethodException(controller.Controller.GetType().FullName, methodName);
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

    private static void AssertMetadataHeadersAbsent(IHeaderDictionary headers)
    {
        headers.ContainsKey(HeaderNames.ETag).ShouldBeFalse();
        headers.ContainsKey("X-Hexalith-Projection-Version").ShouldBeFalse();
        headers.ContainsKey("X-Hexalith-Served-At").ShouldBeFalse();
        headers.ContainsKey("X-Hexalith-Is-Stale").ShouldBeFalse();
        headers.ContainsKey("X-Hexalith-Is-Degraded").ShouldBeFalse();
        headers.ContainsKey("X-Hexalith-Warning-Codes").ShouldBeFalse();
        headers.ContainsKey("X-Hexalith-Page-Size").ShouldBeFalse();
        headers.ContainsKey("X-Hexalith-Page-Offset").ShouldBeFalse();
        headers.ContainsKey("X-Hexalith-Page-Total-Count").ShouldBeFalse();
        headers.ContainsKey("X-Hexalith-Page-Has-More").ShouldBeFalse();
        headers.ContainsKey("X-Hexalith-Next-Cursor").ShouldBeFalse();
    }

    // Fake of the PUBLIC ICommandStatusLocationBuilder — Sample.Tests references Client as a project but has
    // no InternalsVisibleTo access to the real internal CommandStatusLocationBuilder. A null base is
    // fail-closed; a configured base mirrors the real absolute composition against the real compiled controller.
    private sealed class FakeCommandStatusLocationBuilder(string? gatewayStatusBase) : ICommandStatusLocationBuilder
    {
        public bool TryBuild(string statusKey, [NotNullWhen(true)] out string? location)
        {
            if (string.IsNullOrWhiteSpace(gatewayStatusBase))
            {
                location = null;
                return false;
            }

            location = gatewayStatusBase.TrimEnd('/') + "/api/v1/commands/status/" + Uri.EscapeDataString(statusKey);
            return true;
        }
    }

    private sealed record GeneratedController(ControllerBase Controller, FakeEventStoreGatewayClient Gateway);

    private sealed record CommandCase(
        string MethodName,
        object MatchingBody,
        object MismatchedBody,
        string CommandType);
}
