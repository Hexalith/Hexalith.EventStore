using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;

using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Queries;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;

namespace Hexalith.EventStore.RestApi.Generators.Tests;

public sealed class RestApiGeneratedControllerErrorSemanticsTests
{
    [Fact]
    public async Task QueryAction_GatewayForbidden_ReturnsSafeProblemDetails()
    {
        RestApiGeneratedController controller = CreateController();
        controller.Gateway.QueryHandler = static (_, _, _) =>
        {
            IReadOnlyDictionary<string, JsonElement> unsafeExtensions = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
            {
                ["stackTrace"] = JsonSerializer.SerializeToElement("internal stack"),
                ["token"] = JsonSerializer.SerializeToElement("bearer secret"),
                ["cursor"] = JsonSerializer.SerializeToElement("protected cursor"),
                ["etag"] = JsonSerializer.SerializeToElement("\"internal\""),
            };
            IReadOnlyDictionary<string, string> errors = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["name"] = "Name is required.",
                ["cursor"] = "protected cursor",
                ["safe"] = "raw payload fragment",
            };

            throw new EventStoreGatewayException(
                StatusCodes.Status403Forbidden,
                "Forbidden",
                type: "https://hexalith.dev/problems/forbidden",
                detail: "Tenant access denied.",
                correlationId: "01KTESTFORBIDDEN000000000000",
                tenantId: "tenant-a",
                errors: errors,
                reason: "tenant-denied",
                extensions: unsafeExtensions,
                reasonCode: "tenant-denied");
        };

        IActionResult result = await InvokeQueryAsync(controller);

        ObjectResult objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(StatusCodes.Status403Forbidden);
        objectResult.ContentTypes.ShouldContain("application/problem+json");
        ProblemDetails problem = objectResult.Value.ShouldBeOfType<ProblemDetails>();
        problem.Status.ShouldBe(StatusCodes.Status403Forbidden);
        problem.Title.ShouldBe("Forbidden");
        problem.Detail.ShouldBe("Tenant access denied.");
        problem.Extensions["correlationId"].ShouldBe("01KTESTFORBIDDEN000000000000");
        problem.Extensions["tenantId"].ShouldBe("tenant-a");
        problem.Extensions["reason"].ShouldBe("tenant-denied");
        problem.Extensions["reasonCode"].ShouldBe("tenant-denied");
        problem.Extensions.ContainsKey("stackTrace").ShouldBeFalse();
        problem.Extensions.ContainsKey("token").ShouldBeFalse();
        problem.Extensions.ContainsKey("cursor").ShouldBeFalse();
        problem.Extensions.ContainsKey("etag").ShouldBeFalse();
        var safeErrors = problem.Extensions["errors"].ShouldBeAssignableTo<IReadOnlyDictionary<string, string>>();
        safeErrors.ShouldBe(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["name"] = "Name is required.",
        });
    }

    [Fact]
    public async Task QueryAction_GatewayUnavailable_ReturnsSafeProblemDetailsAndRetryHeader()
    {
        RestApiGeneratedController controller = CreateController();
        controller.Gateway.QueryHandler = static (_, _, _) =>
            throw new EventStoreGatewayException(
                StatusCodes.Status503ServiceUnavailable,
                "Service Unavailable",
                detail: "The query gateway is unavailable.",
                correlationId: "01KTESTUNAVAILABLE000000000",
                reason: "gateway-unavailable",
                retryAfter: "5",
                reasonCode: "gateway-unavailable",
                innerException: new HttpRequestException("Connection refused with internal endpoint detail."));

        IActionResult result = await InvokeQueryAsync(controller);

        ObjectResult objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(StatusCodes.Status503ServiceUnavailable);
        objectResult.ContentTypes.ShouldContain("application/problem+json");
        controller.HttpContext.Response.Headers.RetryAfter.ToString().ShouldBe("5");
        ProblemDetails problem = objectResult.Value.ShouldBeOfType<ProblemDetails>();
        problem.Detail.ShouldBe("The query gateway is unavailable.");
        problem.Extensions["correlationId"].ShouldBe("01KTESTUNAVAILABLE000000000");
        problem.Extensions["reason"].ShouldBe("gateway-unavailable");
        problem.Extensions.ContainsKey("stackTrace").ShouldBeFalse();
    }

    [Fact]
    public async Task QueryAction_InvalidCursorFailure_ReturnsSafeBadRequestProblemDetails()
    {
        RestApiGeneratedController controller = CreateController();
        controller.Gateway.QueryHandler = static (_, _, _) =>
            throw new EventStoreGatewayException(
                StatusCodes.Status400BadRequest,
                "Bad Request",
                detail: "The supplied cursor is invalid.",
                correlationId: "01KTESTINVALIDCURSOR00000",
                reason: "invalid-cursor",
                reasonCode: "invalid-cursor");

        IActionResult result = await InvokeQueryAsync(controller);

        ObjectResult objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        objectResult.ContentTypes.ShouldContain("application/problem+json");
        ProblemDetails problem = objectResult.Value.ShouldBeOfType<ProblemDetails>();
        problem.Detail.ShouldBe("The supplied cursor is invalid.");
        problem.Extensions["reason"].ShouldBe("invalid-cursor");
        problem.Extensions["reasonCode"].ShouldBe("invalid-cursor");
    }

    [Fact]
    public async Task QueryAction_NotModified_ForwardsIfNoneMatchAndPreservesAvailableHeaders()
    {
        RestApiGeneratedController controller = CreateController();
        DateTimeOffset servedAt = new(2026, 7, 5, 10, 30, 0, TimeSpan.Zero);
        controller.Gateway.QueryHandler = static (request, ifNoneMatch, gateway) =>
        {
            gateway.LastQueryRequest = request;
            gateway.LastIfNoneMatch = ifNoneMatch;
            return Task.FromResult(new EventStoreQueryResult(
                "01KTESTNOTMODIFIED0000000",
                null,
                IsNotModified: true,
                ETag: "strong-version")
            {
                Metadata = new QueryResponseMetadata(
                    IsStale: false,
                    ProjectionVersion: "42",
                    ServedAt: new DateTimeOffset(2026, 7, 5, 10, 30, 0, TimeSpan.Zero)),
            });
        };

        IActionResult result = await InvokeQueryAsync(controller, "\"strong-version\"");

        StatusCodeResult statusCodeResult = result.ShouldBeOfType<StatusCodeResult>();
        statusCodeResult.StatusCode.ShouldBe(StatusCodes.Status304NotModified);
        controller.Gateway.LastIfNoneMatch.ShouldBe("\"strong-version\"");
        controller.Gateway.LastQueryRequest.ShouldNotBeNull();
        controller.Gateway.LastQueryRequest.Tenant.ShouldBe("tenant-a");
        controller.Gateway.LastQueryRequest.AggregateId.ShouldBe("counter-1");
        controller.Gateway.LastQueryRequest.EntityId.ShouldBe("counter-1");
        IHeaderDictionary headers = controller.HttpContext.Response.Headers;
        headers["ETag"].ToString().ShouldBe("\"strong-version\"");
        headers["X-Hexalith-Projection-Version"].ToString().ShouldBe("42");
        headers["X-Hexalith-Served-At"].ToString().ShouldBe(servedAt.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
        headers["X-Hexalith-Is-Stale"].ToString().ShouldBe("false");
    }

    [Fact]
    public async Task QueryAction_NotModifiedWithoutMetadata_DoesNotSynthesizeMetadataHeaders()
    {
        RestApiGeneratedController controller = CreateController();
        controller.Gateway.QueryHandler = static (request, ifNoneMatch, gateway) =>
        {
            gateway.LastQueryRequest = request;
            gateway.LastIfNoneMatch = ifNoneMatch;
            return Task.FromResult(new EventStoreQueryResult(
                "01KTESTNOTMODIFIED0000000",
                null,
                IsNotModified: true,
                ETag: "strong-version"));
        };

        IActionResult result = await InvokeQueryAsync(controller, "\"strong-version\"");

        StatusCodeResult statusCodeResult = result.ShouldBeOfType<StatusCodeResult>();
        statusCodeResult.StatusCode.ShouldBe(StatusCodes.Status304NotModified);
        controller.Gateway.LastIfNoneMatch.ShouldBe("\"strong-version\"");
        IHeaderDictionary headers = controller.HttpContext.Response.Headers;
        headers["ETag"].ToString().ShouldBe("\"strong-version\"");
        headers.ContainsKey("X-Hexalith-Projection-Version").ShouldBeFalse();
        headers.ContainsKey("X-Hexalith-Served-At").ShouldBeFalse();
        headers.ContainsKey("X-Hexalith-Is-Stale").ShouldBeFalse();
    }

    [Fact]
    public async Task CommandAction_RouteBodyMismatch_ReturnsBadRequestBeforeGatewayCall()
    {
        RestApiGeneratedController controller = CreateController();
        object body = Activator.CreateInstance(
            controller.Assembly.GetType("Smoke.IncrementCounter", throwOnError: true)!,
            "body-counter",
            5)!;

        IActionResult result = await InvokeCommandAsync(controller, "route-counter", body);

        ObjectResult objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        ProblemDetails problem = objectResult.Value.ShouldBeOfType<ProblemDetails>();
        problem.Detail.ShouldBe("Route value 'counterId' does not match the command body.");
        controller.Gateway.CommandCallCount.ShouldBe(0);
    }

    private static RestApiGeneratedController CreateController()
    {
        CSharpCompilation compilation = RestApiGeneratorTestHarness.CreateCompilation(ControllerExerciseSource);
        CSharpCompilation outputCompilation = RestApiGeneratorTestHarness.RunAndUpdateCompilation(
            compilation,
            out _,
            out ImmutableArray<Diagnostic> updateDiagnostics);
        ShouldHaveNoErrors(updateDiagnostics);
        ShouldHaveNoErrors(outputCompilation.GetDiagnostics(TestContext.Current.CancellationToken));

        Assembly assembly = EmitToAssembly(outputCompilation);
        Type controllerType = assembly
            .GetTypes()
            .Single(static type => string.Equals(type.Name, "CounterRestController", StringComparison.Ordinal));
        var gateway = new FakeEventStoreGatewayClient();
        var controller = (ControllerBase)Activator.CreateInstance(controllerType, gateway)!;
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext(),
        };

        return new RestApiGeneratedController(assembly, gateway, controller);
    }

    private static async Task<IActionResult> InvokeQueryAsync(
        RestApiGeneratedController controller,
        string? ifNoneMatch = null)
    {
        MethodInfo method = controller.Controller.GetType().GetMethod("GetCounterStatusQueryQueryAsync")!;
        var task = (Task<IActionResult>)method.Invoke(
            controller.Controller,
            ["tenant-a", "counter-1", ifNoneMatch, TestContext.Current.CancellationToken])!;
        return await task.ConfigureAwait(false);
    }

    private static async Task<IActionResult> InvokeCommandAsync(
        RestApiGeneratedController controller,
        string counterId,
        object body)
    {
        MethodInfo method = controller.Controller.GetType().GetMethod("IncrementCounterCommandAsync")!;
        var task = (Task<IActionResult>)method.Invoke(
            controller.Controller,
            ["tenant-a", counterId, body, TestContext.Current.CancellationToken])!;
        return await task.ConfigureAwait(false);
    }

    private static Assembly EmitToAssembly(CSharpCompilation compilation)
    {
        using var stream = new MemoryStream();
        EmitResult result = compilation.Emit(stream, cancellationToken: TestContext.Current.CancellationToken);
        if (!result.Success)
        {
            string diagnostics = string.Join(Environment.NewLine, result.Diagnostics.Select(static diagnostic => diagnostic.ToString()));
            throw new InvalidOperationException("Generated controller compilation failed:" + Environment.NewLine + diagnostics);
        }

        return Assembly.Load(stream.ToArray());
    }

    private static void ShouldHaveNoErrors(IEnumerable<Diagnostic> diagnostics)
    {
        Diagnostic[] errors = diagnostics
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();
        errors.ShouldBeEmpty(string.Join(Environment.NewLine, errors.Select(static diagnostic => diagnostic.ToString())));
    }

    private const string ControllerExerciseSource = """
        using Hexalith.EventStore.Contracts.Commands;
        using Hexalith.EventStore.Contracts.Queries;
        using Hexalith.EventStore.Contracts.Rest;

        [assembly: RestApi("api/{tenant}/counter", "counter", RestTenantSource.Route)]

        namespace Smoke;

        [RestRoute(RestVerb.Post, "{counterId}/increment")]
        public sealed record IncrementCounter(string CounterId, int Amount) : ICommandContract
        {
            public static string Domain => "counter";
            public static string CommandType => "increment-counter";
            public string AggregateId => CounterId;
        }

        [RestRoute(RestVerb.Get, "{entityId}")]
        public sealed record GetCounterStatusQuery(string EntityId) : IQueryContract
        {
            public static string QueryType => "get-counter-status";
            public static string Domain => "counter";
            public static string ProjectionType => "counter";
        }
        """;
}
