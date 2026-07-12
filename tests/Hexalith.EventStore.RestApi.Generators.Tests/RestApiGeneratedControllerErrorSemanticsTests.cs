using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json;

using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Commands;
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
    public async Task QueryAction_Success_ForwardsBoundedMetadataHeadersAndRawPayload()
    {
        RestApiGeneratedController controller = CreateController();
        DateTimeOffset servedAt = new(2026, 7, 5, 10, 30, 0, TimeSpan.Zero);
        controller.Gateway.QueryHandler = (request, ifNoneMatch, gateway) =>
        {
            gateway.LastQueryRequest = request;
            gateway.LastIfNoneMatch = ifNoneMatch;
            JsonElement payload = JsonSerializer.SerializeToElement(new
            {
                counterId = "counter-1",
                value = 7,
            });

            return Task.FromResult(new EventStoreQueryResult(
                "01KTESTQUERY200000000000",
                payload,
                IsNotModified: false,
                ETag: "strong-version")
            {
                Metadata = new QueryResponseMetadata(
                    IsStale: false,
                    IsDegraded: true,
                    ProjectionVersion: "42",
                    ServedAt: servedAt,
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
                    ])
                {
                    Provenance = QueryResponseProvenance.ProjectionBacked,
                    Lifecycle = ProjectionLifecycleState.Current,
                },
            });
        };

        IActionResult result = await InvokeQueryAsync(controller);

        OkObjectResult ok = result.ShouldBeOfType<OkObjectResult>();
        JsonElement payload = ok.Value.ShouldBeOfType<JsonElement>();
        payload.GetProperty("counterId").GetString().ShouldBe("counter-1");
        payload.GetProperty("value").GetInt32().ShouldBe(7);
        controller.Gateway.LastIfNoneMatch.ShouldBeNull();
        IHeaderDictionary headers = controller.HttpContext.Response.Headers;
        headers["ETag"].ToString().ShouldBe("\"strong-version\"");
        headers["X-Hexalith-Query-Provenance"].ToString().ShouldBe("ProjectionBacked");
        headers[ProjectionLifecyclePolicy.HeaderName].ToString().ShouldBe("Current");
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

    [Theory]
    [InlineData(ProjectionLifecycleState.Current)]
    [InlineData(ProjectionLifecycleState.Stale)]
    [InlineData(ProjectionLifecycleState.Rebuilding)]
    [InlineData(ProjectionLifecycleState.Degraded)]
    [InlineData(ProjectionLifecycleState.Unavailable)]
    [InlineData(ProjectionLifecycleState.LocalOnly)]
    public async Task QueryAction_ProjectionLifecycle_EmitsExactCanonicalName(ProjectionLifecycleState lifecycle)
    {
        RestApiGeneratedController controller = CreateController();
        controller.Gateway.QueryHandler = (_, _, _) =>
            Task.FromResult(new EventStoreQueryResult(
                "01KTESTQUERY200000000000",
                JsonSerializer.SerializeToElement(new { counterId = "counter-1" }),
                IsNotModified: false,
                ETag: null)
            {
                Metadata = new QueryResponseMetadata
                {
                    Provenance = QueryResponseProvenance.ProjectionBacked,
                    Lifecycle = lifecycle,
                },
            });

        _ = (await InvokeQueryAsync(controller)).ShouldBeOfType<OkObjectResult>();

        controller.HttpContext.Response.Headers[ProjectionLifecyclePolicy.HeaderName]
            .ToString().ShouldBe(lifecycle.ToString());
    }

    [Fact]
    public async Task QueryAction_SuccessWithoutMetadata_DoesNotSynthesizeMetadataHeaders()
    {
        RestApiGeneratedController controller = CreateController();
        controller.HttpContext.Response.Headers[ProjectionLifecyclePolicy.HeaderName] = "Current";
        controller.Gateway.QueryHandler = static (request, ifNoneMatch, gateway) =>
        {
            gateway.LastQueryRequest = request;
            gateway.LastIfNoneMatch = ifNoneMatch;
            JsonElement payload = JsonSerializer.SerializeToElement(new { counterId = "counter-1" });
            return Task.FromResult(new EventStoreQueryResult(
                "01KTESTQUERY200000000000",
                payload,
                IsNotModified: false,
                ETag: null));
        };

        IActionResult result = await InvokeQueryAsync(controller);

        _ = result.ShouldBeOfType<OkObjectResult>();
        AssertMetadataHeadersAbsent(controller.HttpContext.Response.Headers);
    }

    [Theory]
    [InlineData(ProjectionLifecycleState.Unknown)]
    [InlineData((ProjectionLifecycleState)99)]
    public async Task QueryAction_UnknownOrInvalidLifecycle_RemovesPreExistingHeader(
        ProjectionLifecycleState lifecycle)
    {
        RestApiGeneratedController controller = CreateController();
        controller.HttpContext.Response.Headers[ProjectionLifecyclePolicy.HeaderName] = "Stale";
        controller.Gateway.QueryHandler = (_, _, _) =>
            Task.FromResult(new EventStoreQueryResult(
                "01KTESTQUERY200000000000",
                JsonSerializer.SerializeToElement(new { counterId = "counter-1" }),
                IsNotModified: false,
                ETag: null)
            {
                Metadata = new QueryResponseMetadata
                {
                    Provenance = QueryResponseProvenance.ProjectionBacked,
                    Lifecycle = lifecycle,
                },
            });

        _ = (await InvokeQueryAsync(controller)).ShouldBeOfType<OkObjectResult>();

        controller.HttpContext.Response.Headers.ContainsKey(ProjectionLifecyclePolicy.HeaderName).ShouldBeFalse();
    }

    [Theory]
    [InlineData(ProjectionLifecycleState.Current, true, false, "false", "false")]
    [InlineData(ProjectionLifecycleState.Stale, false, true, "true", "true")]
    [InlineData(ProjectionLifecycleState.Rebuilding, false, true, null, "true")]
    [InlineData(ProjectionLifecycleState.Degraded, false, false, null, "true")]
    public async Task QueryAction_Lifecycle_NormalizesCompatibilityHeaders(
        ProjectionLifecycleState lifecycle,
        bool staleFallback,
        bool degradedFallback,
        string? expectedStale,
        string expectedDegraded)
    {
        RestApiGeneratedController controller = CreateController();
        controller.Gateway.QueryHandler = (_, _, _) =>
            Task.FromResult(new EventStoreQueryResult(
                "01KTESTQUERY200000000000",
                JsonSerializer.SerializeToElement(new { counterId = "counter-1" }),
                IsNotModified: false,
                ETag: null)
            {
                Metadata = new QueryResponseMetadata(IsStale: staleFallback, IsDegraded: degradedFallback)
                {
                    Provenance = QueryResponseProvenance.ProjectionBacked,
                    Lifecycle = lifecycle,
                },
            });

        _ = (await InvokeQueryAsync(controller)).ShouldBeOfType<OkObjectResult>();

        IHeaderDictionary headers = controller.HttpContext.Response.Headers;
        headers["X-Hexalith-Is-Stale"].ToString().ShouldBe(expectedStale ?? string.Empty);
        headers["X-Hexalith-Is-Degraded"].ToString().ShouldBe(expectedDegraded);
    }

    [Fact]
    public async Task QueryAction_SuccessWithMetadataETag_ForwardsStrongETagHeader()
    {
        RestApiGeneratedController controller = CreateController();
        controller.Gateway.QueryHandler = static (_, _, _) =>
        {
            JsonElement payload = JsonSerializer.SerializeToElement(new { counterId = "counter-1" });
            return Task.FromResult(new EventStoreQueryResult(
                "01KTESTQUERY200000000000",
                payload,
                IsNotModified: false,
                ETag: null)
            {
                Metadata = new QueryResponseMetadata(ETag: "metadata-version")
                {
                    Provenance = QueryResponseProvenance.ProjectionBacked,
                },
            });
        };

        IActionResult result = await InvokeQueryAsync(controller);

        _ = result.ShouldBeOfType<OkObjectResult>();
        controller.HttpContext.Response.Headers["ETag"].ToString().ShouldBe("\"metadata-version\"");
    }

    [Theory]
    [InlineData(QueryResponseProvenance.HandlerComputed, "HandlerComputed")]
    [InlineData(QueryResponseProvenance.Unknown, "Unknown")]
    public async Task QueryAction_NonProjectionProvenance_OmitsProjectionEvidenceAndPreservesSafeMetadata(
        QueryResponseProvenance provenance,
        string expectedHeader)
    {
        RestApiGeneratedController controller = CreateController();
        DateTimeOffset servedAt = new(2026, 7, 11, 10, 0, 0, TimeSpan.Zero);
        controller.Gateway.QueryHandler = (_, _, _) =>
        {
            JsonElement payload = JsonSerializer.SerializeToElement(new { counterId = "counter-1" });
            return Task.FromResult(new EventStoreQueryResult(
                "01KTESTQUERY200000000000",
                payload,
                IsNotModified: false,
                ETag: "contradictory-etag")
            {
                Metadata = new QueryResponseMetadata(
                    ETag: "metadata-etag",
                    IsNotModified: true,
                    IsStale: false,
                    IsDegraded: true,
                    ProjectionVersion: "v9",
                    ServedAt: servedAt,
                    Paging: new QueryPagingMetadata(PageSize: 25, HasMore: false),
                    WarningCodes: [QueryWarningCodes.DegradedSearch])
                {
                    Provenance = provenance,
                    Lifecycle = ProjectionLifecycleState.Current,
                },
            });
        };

        IActionResult result = await InvokeQueryAsync(controller);

        _ = result.ShouldBeOfType<OkObjectResult>();
        IHeaderDictionary headers = controller.HttpContext.Response.Headers;
        headers["X-Hexalith-Query-Provenance"].ToString().ShouldBe(expectedHeader);
        headers.ContainsKey("ETag").ShouldBeFalse();
        headers.ContainsKey("X-Hexalith-Projection-Version").ShouldBeFalse();
        headers.ContainsKey("X-Hexalith-Is-Stale").ShouldBeFalse();
        headers.ContainsKey(ProjectionLifecyclePolicy.HeaderName).ShouldBeFalse();
        headers["X-Hexalith-Served-At"].ToString().ShouldBe(servedAt.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
        headers["X-Hexalith-Is-Degraded"].ToString().ShouldBe("true");
        headers["X-Hexalith-Warning-Codes"].ToString().ShouldBe(QueryWarningCodes.DegradedSearch);
        headers["X-Hexalith-Page-Size"].ToString().ShouldBe("25");
        headers["X-Hexalith-Page-Has-More"].ToString().ShouldBe("false");
    }

    [Fact]
    public async Task QueryAction_SuccessWithOversizedOrUnsafeMetadata_OmitsOnlyInvalidHeaders()
    {
        RestApiGeneratedController controller = CreateController();
        string oversizedValue = new('x', QueryPolicyLimits.MaxCursorLength + 1);
        string unsafeCursorValue = "unsafe\r\nvalue";
        controller.Gateway.QueryHandler = (_, _, _) =>
        {
            JsonElement payload = JsonSerializer.SerializeToElement(new { counterId = "counter-1" });
            return Task.FromResult(new EventStoreQueryResult(
                "01KTESTQUERY200000000000",
                payload,
                IsNotModified: false,
                ETag: "W/\"weak-version\"")
            {
                Metadata = new QueryResponseMetadata(
                    ProjectionVersion: oversizedValue,
                    Paging: new QueryPagingMetadata(
                        PageSize: QueryPolicyLimits.MaxPageSize + 1,
                        Offset: -1,
                        NextCursor: unsafeCursorValue,
                        TotalCount: -1,
                        HasMore: true),
                    WarningCodes: Enumerable
                        .Range(0, 17)
                        .Select(static value => "warning_" + value.ToString(System.Globalization.CultureInfo.InvariantCulture))
                        .ToArray()),
            });
        };

        IActionResult result = await InvokeQueryAsync(controller);

        _ = result.ShouldBeOfType<OkObjectResult>();
        IHeaderDictionary headers = controller.HttpContext.Response.Headers;
        headers.ContainsKey("ETag").ShouldBeFalse();
        headers.ContainsKey("X-Hexalith-Projection-Version").ShouldBeFalse();
        headers.ContainsKey("X-Hexalith-Warning-Codes").ShouldBeFalse();
        headers.ContainsKey("X-Hexalith-Next-Cursor").ShouldBeFalse();
        headers.ContainsKey("X-Hexalith-Page-Size").ShouldBeFalse();
        headers.ContainsKey("X-Hexalith-Page-Offset").ShouldBeFalse();
        headers.ContainsKey("X-Hexalith-Page-Total-Count").ShouldBeFalse();
        headers["X-Hexalith-Page-Has-More"].ToString().ShouldBe("true");
    }

    [Fact]
    public async Task QueryAction_SuccessWithInvalidWarningCodeToken_OmitsWarningCodeHeader()
    {
        RestApiGeneratedController controller = CreateController();
        controller.Gateway.QueryHandler = (_, _, _) =>
        {
            JsonElement payload = JsonSerializer.SerializeToElement(new { counterId = "counter-1" });
            return Task.FromResult(new EventStoreQueryResult(
                "01KTESTQUERY200000000000",
                payload,
                IsNotModified: false,
                ETag: "strong-version")
            {
                Metadata = new QueryResponseMetadata(
                    WarningCodes:
                    [
                        QueryWarningCodes.DegradedSearch,
                        "dégradé",
                    ])
                {
                    Provenance = QueryResponseProvenance.ProjectionBacked,
                },
            });
        };

        IActionResult result = await InvokeQueryAsync(controller);

        _ = result.ShouldBeOfType<OkObjectResult>();
        IHeaderDictionary headers = controller.HttpContext.Response.Headers;
        headers["ETag"].ToString().ShouldBe("\"strong-version\"");
        headers.ContainsKey("X-Hexalith-Warning-Codes").ShouldBeFalse();
    }

    [Theory]
    [InlineData("bad\"etag")]
    [InlineData("bad etag")]
    [InlineData("\"bad etag\"")]
    [InlineData("W/\"weak-version\"")]
    [InlineData(" strong-version")]
    [InlineData("strong-version ")]
    public async Task QueryAction_SuccessWithInvalidETag_OmitsETagHeader(string etag)
    {
        RestApiGeneratedController controller = CreateController();
        controller.Gateway.QueryHandler = (_, _, _) =>
        {
            JsonElement payload = JsonSerializer.SerializeToElement(new { counterId = "counter-1" });
            return Task.FromResult(new EventStoreQueryResult(
                "01KTESTQUERY200000000000",
                payload,
                IsNotModified: false,
                ETag: etag));
        };

        IActionResult result = await InvokeQueryAsync(controller);

        _ = result.ShouldBeOfType<OkObjectResult>();
        controller.HttpContext.Response.Headers.ContainsKey("ETag").ShouldBeFalse();
    }

    [Fact]
    public async Task QueryAction_GatewayUnsafeTopLevelProblemFields_NeutralizesUnsafeProblemDetails()
    {
        RestApiGeneratedController controller = CreateController();
        controller.Gateway.QueryHandler = static (_, _, _) =>
            throw new EventStoreGatewayException(
                StatusCodes.Status500InternalServerError,
                "Bearer token leaked",
                type: "https://hexalith.dev/problems/token-leak",
                detail: "invalid cursor eyJwYWdlIjoxLCJza2lwIjoyNX0",
                correlationId: "unsafe cursor metadata",
                tenantId: "tenant payload fragment",
                reason: "bad reason",
                retryAfter: "bearer-secret",
                reasonCode: "bad,code");

        IActionResult result = await InvokeQueryAsync(controller);

        ObjectResult objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(StatusCodes.Status500InternalServerError);
        objectResult.ContentTypes.ShouldContain("application/problem+json");
        controller.HttpContext.Response.Headers.ContainsKey("Retry-After").ShouldBeFalse();
        ProblemDetails problem = objectResult.Value.ShouldBeOfType<ProblemDetails>();
        problem.Status.ShouldBe(StatusCodes.Status500InternalServerError);
        problem.Title.ShouldBe("Gateway Error");
        problem.Type.ShouldBe("about:blank");
        problem.Detail.ShouldBeNull();
        problem.Extensions.ContainsKey("correlationId").ShouldBeFalse();
        problem.Extensions.ContainsKey("tenantId").ShouldBeFalse();
        problem.Extensions.ContainsKey("reason").ShouldBeFalse();
        problem.Extensions.ContainsKey("reasonCode").ShouldBeFalse();
    }

    [Fact]
    public async Task QueryAction_GatewayOversizedTopLevelProblemFields_NeutralizesUnsafeProblemDetails()
    {
        RestApiGeneratedController controller = CreateController();
        string oversizedDetail = new('x', QueryPolicyLimits.MaxCursorLength + 1);
        controller.Gateway.QueryHandler = (_, _, _) =>
            throw new EventStoreGatewayException(
                StatusCodes.Status503ServiceUnavailable,
                "Service Unavailable",
                detail: oversizedDetail,
                reason: "gateway-unavailable",
                reasonCode: "gateway-unavailable");

        IActionResult result = await InvokeQueryAsync(controller);

        ObjectResult objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(StatusCodes.Status503ServiceUnavailable);
        ProblemDetails problem = objectResult.Value.ShouldBeOfType<ProblemDetails>();
        problem.Title.ShouldBe("Service Unavailable");
        problem.Detail.ShouldBeNull();
        problem.Extensions["reason"].ShouldBe("gateway-unavailable");
    }

    [Fact]
    public async Task QueryAction_GatewayForbidden_ReturnsSafeProblemDetails()
    {
        RestApiGeneratedController controller = CreateController();
        string oversizedError = new('x', QueryPolicyLimits.MaxCursorLength + 1);
        controller.Gateway.QueryHandler = (_, _, _) =>
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
                ["control"] = "line1\r\nline2",
                ["oversized"] = oversizedError,
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

    [Theory]
    [InlineData("-1", false)]
    [InlineData("86400", true)]
    [InlineData("86401", false)]
    [InlineData("9223372036854775807", false)]
    [InlineData("999999999999999999999999999999", false)]
    [InlineData("Tue, 07 Jul 2026 10:30:00 GMT", true)]
    public async Task QueryAction_GatewayUnavailable_EmitsOnlyValidRetryAfterHeaderValues(
        string retryAfter,
        bool expectHeader)
    {
        RestApiGeneratedController controller = CreateController();
        controller.Gateway.QueryHandler = (_, _, _) =>
            throw new EventStoreGatewayException(
                StatusCodes.Status503ServiceUnavailable,
                "Service Unavailable",
                detail: "The query gateway is unavailable.",
                retryAfter: retryAfter);

        IActionResult result = await InvokeQueryAsync(controller);

        _ = result.ShouldBeOfType<ObjectResult>();
        IHeaderDictionary headers = controller.HttpContext.Response.Headers;
        if (expectHeader)
        {
            headers["Retry-After"].ToString().ShouldBe(retryAfter);
        }
        else
        {
            headers.ContainsKey("Retry-After").ShouldBeFalse();
        }
    }

    [Fact]
    public async Task QueryAction_GatewayValidationFailure_DoesNotEmitRetryAfterAndCapsErrors()
    {
        RestApiGeneratedController controller = CreateController();
        IReadOnlyDictionary<string, string> errors = Enumerable
            .Range(0, 20)
            .ToDictionary(
                static value => "field" + value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                static value => "Value " + value.ToString(System.Globalization.CultureInfo.InvariantCulture) + " is invalid.",
                StringComparer.Ordinal);
        controller.Gateway.QueryHandler = (_, _, _) =>
            throw new EventStoreGatewayException(
                StatusCodes.Status400BadRequest,
                "Bad Request",
                detail: "The query request is invalid.",
                errors: errors,
                retryAfter: "5");

        IActionResult result = await InvokeQueryAsync(controller);

        ObjectResult objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        controller.HttpContext.Response.Headers.ContainsKey("Retry-After").ShouldBeFalse();
        ProblemDetails problem = objectResult.Value.ShouldBeOfType<ProblemDetails>();
        var safeErrors = problem.Extensions["errors"].ShouldBeAssignableTo<IReadOnlyDictionary<string, string>>();
        safeErrors.Count.ShouldBe(16);
        safeErrors.ContainsKey("field0").ShouldBeTrue();
        safeErrors.ContainsKey("field15").ShouldBeTrue();
        safeErrors.ContainsKey("field16").ShouldBeFalse();
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
        problem.Detail.ShouldBeNull();
        problem.Extensions["reason"].ShouldBe("invalid-cursor");
        problem.Extensions["reasonCode"].ShouldBe("invalid-cursor");
    }

    [Fact]
    public async Task QueryAction_SemanticGatewayFailureWithSuccessStatus_ReturnsBadGatewayProblemDetails()
    {
        RestApiGeneratedController controller = CreateController();
        controller.Gateway.QueryHandler = static (_, _, _) =>
            throw new EventStoreGatewayException(
                StatusCodes.Status200OK,
                "Query semantic failure",
                detail: "Query response reported failure.");

        IActionResult result = await InvokeQueryAsync(controller);

        ObjectResult objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(StatusCodes.Status502BadGateway);
        objectResult.ContentTypes.ShouldContain("application/problem+json");
        ProblemDetails problem = objectResult.Value.ShouldBeOfType<ProblemDetails>();
        problem.Status.ShouldBe(StatusCodes.Status502BadGateway);
        problem.Title.ShouldBe("Query semantic failure");
        problem.Detail.ShouldBe("Query response reported failure.");
    }

    [Fact]
    public async Task QueryAction_GatewayNotImplemented_ReturnsSafeProblemDetails()
    {
        RestApiGeneratedController controller = CreateController();
        controller.Gateway.QueryHandler = static (_, _, _) =>
            throw new EventStoreGatewayException(
                StatusCodes.Status501NotImplemented,
                "Not Implemented",
                detail: "The query handler is not implemented.",
                reason: "query-not-implemented",
                reasonCode: "query-not-implemented");

        IActionResult result = await InvokeQueryAsync(controller);

        ObjectResult objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(StatusCodes.Status501NotImplemented);
        objectResult.ContentTypes.ShouldContain("application/problem+json");
        ProblemDetails problem = objectResult.Value.ShouldBeOfType<ProblemDetails>();
        problem.Status.ShouldBe(StatusCodes.Status501NotImplemented);
        problem.Detail.ShouldBe("The query handler is not implemented.");
        problem.Extensions["reason"].ShouldBe("query-not-implemented");
        problem.Extensions["reasonCode"].ShouldBe("query-not-implemented");
    }

    [Fact]
    public async Task QueryAction_GatewayNotFound_ReturnsSafeProblemDetails()
    {
        RestApiGeneratedController controller = CreateController();
        controller.Gateway.QueryHandler = static (_, _, _) =>
            throw new EventStoreGatewayException(
                StatusCodes.Status404NotFound,
                "Not Found",
                detail: "The requested counter was not found.",
                correlationId: "01KTESTNOTFOUND000000000",
                tenantId: "tenant-a",
                reason: "query-not-found",
                reasonCode: "query-not-found");

        IActionResult result = await InvokeQueryAsync(controller);

        ObjectResult objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
        objectResult.ContentTypes.ShouldContain("application/problem+json");
        ProblemDetails problem = objectResult.Value.ShouldBeOfType<ProblemDetails>();
        problem.Status.ShouldBe(StatusCodes.Status404NotFound);
        problem.Title.ShouldBe("Not Found");
        problem.Detail.ShouldBe("The requested counter was not found.");
        problem.Extensions["correlationId"].ShouldBe("01KTESTNOTFOUND000000000");
        problem.Extensions["tenantId"].ShouldBe("tenant-a");
        problem.Extensions["reason"].ShouldBe("query-not-found");
        problem.Extensions["reasonCode"].ShouldBe("query-not-found");
    }

    [Fact]
    public async Task QueryAction_GatewayValidationFailure_ReturnsSafeValidationProblemDetails()
    {
        RestApiGeneratedController controller = CreateController();
        IReadOnlyDictionary<string, string> errors = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["pageSize"] = "Page size is invalid.",
            ["cursor"] = "protected cursor",
            ["payload"] = "raw payload fragment",
        };
        controller.Gateway.QueryHandler = (_, _, _) =>
            throw new EventStoreGatewayException(
                StatusCodes.Status400BadRequest,
                "Bad Request",
                detail: "The query request is invalid.",
                correlationId: "01KTESTVALIDATION0000000",
                errors: errors,
                reason: "query-validation",
                reasonCode: "query-validation");

        IActionResult result = await InvokeQueryAsync(controller);

        ObjectResult objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        objectResult.ContentTypes.ShouldContain("application/problem+json");
        ProblemDetails problem = objectResult.Value.ShouldBeOfType<ProblemDetails>();
        problem.Status.ShouldBe(StatusCodes.Status400BadRequest);
        problem.Detail.ShouldBe("The query request is invalid.");
        problem.Extensions["reason"].ShouldBe("query-validation");
        problem.Extensions["reasonCode"].ShouldBe("query-validation");
        var safeErrors = problem.Extensions["errors"].ShouldBeAssignableTo<IReadOnlyDictionary<string, string>>();
        safeErrors.ShouldBe(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["pageSize"] = "Page size is invalid.",
        });
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
                    IsDegraded: true,
                    ProjectionVersion: "42",
                    ServedAt: new DateTimeOffset(2026, 7, 5, 10, 30, 0, TimeSpan.Zero),
                    Paging: new QueryPagingMetadata(
                        PageSize: 25,
                        Offset: 50,
                        NextCursor: "opaque-next",
                        TotalCount: 125,
                        HasMore: true),
                    WarningCodes: [QueryWarningCodes.DegradedSearch])
                {
                    Provenance = QueryResponseProvenance.ProjectionBacked,
                    Lifecycle = ProjectionLifecycleState.Degraded,
                },
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
        headers["X-Hexalith-Query-Provenance"].ToString().ShouldBe("ProjectionBacked");
        headers[ProjectionLifecyclePolicy.HeaderName].ToString().ShouldBe("Degraded");
        headers["X-Hexalith-Projection-Version"].ToString().ShouldBe("42");
        headers["X-Hexalith-Served-At"].ToString().ShouldBe(servedAt.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
        headers["X-Hexalith-Is-Stale"].ToString().ShouldBeEmpty();
        headers["X-Hexalith-Is-Degraded"].ToString().ShouldBe("true");
        headers["X-Hexalith-Warning-Codes"].ToString().ShouldBe("degraded_search");
        headers["X-Hexalith-Page-Size"].ToString().ShouldBe("25");
        headers["X-Hexalith-Page-Offset"].ToString().ShouldBe("50");
        headers["X-Hexalith-Page-Total-Count"].ToString().ShouldBe("125");
        headers["X-Hexalith-Page-Has-More"].ToString().ShouldBe("true");
        headers["X-Hexalith-Next-Cursor"].ToString().ShouldBe("opaque-next");
    }

    [Fact]
    public async Task QueryAction_NotModifiedWithoutMetadata_FailsClosed()
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

        ObjectResult objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(StatusCodes.Status502BadGateway);
        controller.Gateway.LastIfNoneMatch.ShouldBe("\"strong-version\"");
        IHeaderDictionary headers = controller.HttpContext.Response.Headers;
        headers.ContainsKey("ETag").ShouldBeFalse();
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

    [Fact]
    public async Task QueryAction_NotModifiedWithMetadataETag_ReturnsNotModified()
    {
        RestApiGeneratedController controller = CreateController();
        controller.Gateway.QueryHandler = static (_, _, _) =>
            Task.FromResult(new EventStoreQueryResult(
                "01KTESTNOTMODIFIED0000000",
                null,
                IsNotModified: true,
                ETag: null)
            {
                Metadata = new QueryResponseMetadata(ETag: "metadata-version")
                {
                    Provenance = QueryResponseProvenance.ProjectionBacked,
                },
            });

        IActionResult result = await InvokeQueryAsync(controller, "\"metadata-version\"");

        StatusCodeResult statusCodeResult = result.ShouldBeOfType<StatusCodeResult>();
        statusCodeResult.StatusCode.ShouldBe(StatusCodes.Status304NotModified);
        controller.HttpContext.Response.Headers["ETag"].ToString().ShouldBe("\"metadata-version\"");
    }

    [Fact]
    public async Task QueryAction_NotModifiedWithoutStrongETag_ReturnsBadGatewayProblemDetails()
    {
        RestApiGeneratedController controller = CreateController();
        controller.Gateway.QueryHandler = static (_, _, _) =>
            Task.FromResult(new EventStoreQueryResult(
                "01KTESTNOTMODIFIED0000000",
                null,
                IsNotModified: true,
                ETag: "W/\"weak-version\"")
            {
                Metadata = new QueryResponseMetadata(
                    IsStale: false,
                    ProjectionVersion: "42",
                    ServedAt: new DateTimeOffset(2026, 7, 5, 10, 30, 0, TimeSpan.Zero))
                {
                    Provenance = QueryResponseProvenance.ProjectionBacked,
                },
            });

        IActionResult result = await InvokeQueryAsync(controller, "\"weak-version\"");

        ObjectResult objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(StatusCodes.Status502BadGateway);
        ProblemDetails problem = objectResult.Value.ShouldBeOfType<ProblemDetails>();
        problem.Detail.ShouldBe("Not-modified query response requires a strong ETag.");
        AssertMetadataHeadersAbsent(controller.HttpContext.Response.Headers);
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

    [Fact]
    public async Task CommandAction_NullBody_ReturnsBadRequestBeforeGatewayCall()
    {
        RestApiGeneratedController controller = CreateController();

        IActionResult result = await InvokeCommandAsync(controller, "counter-1", null);

        ObjectResult objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        objectResult.ContentTypes.ShouldContain("application/problem+json");
        ProblemDetails problem = objectResult.Value.ShouldBeOfType<ProblemDetails>();
        problem.Detail.ShouldBe("Request body is required.");
        controller.Gateway.CommandCallCount.ShouldBe(0);
    }

    [Fact]
    public async Task CommandAction_GatewayFailure_ReturnsProblemDetailsWithoutSuccessHeaders()
    {
        RestApiGeneratedController controller = CreateController();
        controller.Gateway.CommandHandler = (_, _) =>
            throw new EventStoreGatewayException(
                StatusCodes.Status503ServiceUnavailable,
                "Service Unavailable",
                detail: "The command gateway is unavailable.",
                correlationId: "01KTESTCOMMANDFAIL000000",
                reason: "gateway-unavailable",
                retryAfter: "5",
                reasonCode: "gateway-unavailable");
        object body = Activator.CreateInstance(
            controller.Assembly.GetType("Smoke.IncrementCounter", throwOnError: true)!,
            "counter-1",
            5)!;

        IActionResult result = await InvokeCommandAsync(controller, "counter-1", body);

        ObjectResult objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(StatusCodes.Status503ServiceUnavailable);
        objectResult.ContentTypes.ShouldContain("application/problem+json");
        ProblemDetails problem = objectResult.Value.ShouldBeOfType<ProblemDetails>();
        problem.Detail.ShouldBe("The command gateway is unavailable.");
        problem.Extensions["correlationId"].ShouldBe("01KTESTCOMMANDFAIL000000");
        problem.Extensions["reason"].ShouldBe("gateway-unavailable");
        controller.Gateway.CommandCallCount.ShouldBe(1);
        IHeaderDictionary headers = controller.HttpContext.Response.Headers;
        headers["Retry-After"].ToString().ShouldBe("5");
        headers.ContainsKey("Location").ShouldBeFalse();
    }

    [Fact]
    public async Task CommandAction_ValidBody_WithConfiguredStatusBase_SubmitsGatewayCommandAndReturnsAbsoluteLocation()
    {
        RestApiGeneratedController controller = CreateController(
            new FakeCommandStatusLocationBuilder("https://gateway.example"));
        SubmitCommandRequest? capturedRequest = null;
        controller.Gateway.CommandHandler = (request, _) =>
        {
            capturedRequest = request;
            return Task.FromResult(new SubmitCommandResponse(
                "01KTESTCOMMANDCORRELATION000",
                MessageId: "01KTESTCOMMANDSTATUS000000"));
        };
        object body = Activator.CreateInstance(
            controller.Assembly.GetType("Smoke.IncrementCounter", throwOnError: true)!,
            "counter-1",
            5)!;

        IActionResult result = await InvokeCommandAsync(controller, "counter-1", body);

        AcceptedResult accepted = result.ShouldBeOfType<AcceptedResult>();
        SubmitCommandResponse response = accepted.Value.ShouldBeOfType<SubmitCommandResponse>();
        response.CorrelationId.ShouldBe("01KTESTCOMMANDCORRELATION000");
        response.MessageId.ShouldBe("01KTESTCOMMANDSTATUS000000");
        controller.Gateway.CommandCallCount.ShouldBe(1);
        SubmitCommandRequest request = capturedRequest.ShouldNotBeNull();
        request.MessageId.ShouldNotBeNullOrWhiteSpace();
        request.Tenant.ShouldBe("tenant-a");
        request.Domain.ShouldBe("counter");
        request.AggregateId.ShouldBe("counter-1");
        request.CommandType.ShouldBe("increment-counter");
        request.Payload.GetProperty("counterId").GetString().ShouldBe("counter-1");
        request.Payload.GetProperty("amount").GetInt32().ShouldBe(5);
        IHeaderDictionary headers = controller.HttpContext.Response.Headers;
        headers["Retry-After"].ToString().ShouldBe("1");
        headers["Location"].ToString().ShouldBe("https://gateway.example/api/v1/commands/status/01KTESTCOMMANDSTATUS000000");
        headers["Location"].ToString().ShouldNotStartWith("/");
    }

    [Fact]
    public async Task CommandAction_ValidBody_WithoutConfiguredStatusBase_ReturnsAcceptedAndOmitsLocation()
    {
        RestApiGeneratedController controller = CreateController();
        controller.Gateway.CommandHandler = (_, _) =>
            Task.FromResult(new SubmitCommandResponse("01KTESTCOMMANDSTATUS000000"));
        object body = Activator.CreateInstance(
            controller.Assembly.GetType("Smoke.IncrementCounter", throwOnError: true)!,
            "counter-1",
            5)!;

        IActionResult result = await InvokeCommandAsync(controller, "counter-1", body);

        AcceptedResult accepted = result.ShouldBeOfType<AcceptedResult>();
        SubmitCommandResponse response = accepted.Value.ShouldBeOfType<SubmitCommandResponse>();
        response.CorrelationId.ShouldBe("01KTESTCOMMANDSTATUS000000");
        controller.Gateway.CommandCallCount.ShouldBe(1);
        IHeaderDictionary headers = controller.HttpContext.Response.Headers;
        headers["Retry-After"].ToString().ShouldBe("1");
        headers.ContainsKey("Location").ShouldBeFalse();
    }

    private static RestApiGeneratedController CreateController(ICommandStatusLocationBuilder? statusLocationBuilder = null)
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
        var controller = (ControllerBase)Activator.CreateInstance(
            controllerType,
            gateway,
            statusLocationBuilder ?? new FakeCommandStatusLocationBuilder(gatewayStatusBase: null))!;
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
        object? body)
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

    private static void AssertMetadataHeadersAbsent(IHeaderDictionary headers)
    {
        headers.ContainsKey("ETag").ShouldBeFalse();
        headers.ContainsKey(ProjectionLifecyclePolicy.HeaderName).ShouldBeFalse();
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

    private static void ShouldHaveNoErrors(IEnumerable<Diagnostic> diagnostics)
    {
        Diagnostic[] errors = diagnostics
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();
        errors.ShouldBeEmpty(string.Join(Environment.NewLine, errors.Select(static diagnostic => diagnostic.ToString())));
    }

    // Fake of the PUBLIC ICommandStatusLocationBuilder — this project references Client as a project but
    // has no InternalsVisibleTo access to the real internal CommandStatusLocationBuilder. A null base is
    // fail-closed; a configured base mirrors the real absolute composition.
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
