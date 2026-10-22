using System.Text.Json;

using FluentValidation.Results;

using Hexalith.EventStore.Contracts.Authorization;
using Hexalith.EventStore.Contracts.Problems;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.ErrorHandling;
using Hexalith.EventStore.Middleware;
using Hexalith.EventStore.Server.Queries;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.ErrorHandling;

public class QueryProblemDetailsTests {
    [Fact]
    public void ValidationProblemDetailsFactory_WithQueryPolicyFailure_AddsStableReasonCode() {
        var failure = new ValidationFailure("Paging.PageSize", "PageSize cannot exceed the maximum.") {
            ErrorCode = QueryProblemReasonCodes.InvalidPage,
        };

        ProblemDetails problem = ValidationProblemDetailsFactory.Create(
            "The query has 1 validation error.",
            [failure],
            "corr-1",
            "tenant-a");

        problem.Type.ShouldBe(ProblemTypeUris.ValidationError);
        GetExtensionString(problem, GatewayProblemDetailsExtensions.ReasonCode)
            .ShouldBe(QueryProblemReasonCodes.InvalidPage);
    }

    [Fact]
    public async Task QueryNotFoundExceptionHandler_ResponseBody_IncludesProjectionMissingReasonCode() {
        var handler = new QueryNotFoundExceptionHandler(NullLogger<QueryNotFoundExceptionHandler>.Instance);
        DefaultHttpContext httpContext = CreateHttpContext();

        _ = await handler.TryHandleAsync(
            httpContext,
            new QueryNotFoundException("tenant-a", "party", "party-1", "GetParty"),
            CancellationToken.None);

        ProblemDetails problem = await ReadProblemDetailsAsync(httpContext);

        problem.Type.ShouldBe(ProblemTypeUris.NotFound);
        GetExtensionString(problem, GatewayProblemDetailsExtensions.ReasonCode)
            .ShouldBe(QueryProblemReasonCodes.ProjectionMissing);
    }

    [Fact]
    public async Task QueryExecutionFailedExceptionHandler_NotImplemented_IncludesQueryReasonCode() {
        var handler = new QueryExecutionFailedExceptionHandler(NullLogger<QueryExecutionFailedExceptionHandler>.Instance);
        DefaultHttpContext httpContext = CreateHttpContext();
        var exception = new QueryExecutionFailedException(
            "corr-1",
            "tenant-a",
            "party",
            "party-1",
            "SearchParties",
            StatusCodes.Status501NotImplemented,
            "Query type is not implemented.",
            QueryProblemReasonCodes.NotImplemented);

        _ = await handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        ProblemDetails problem = await ReadProblemDetailsAsync(httpContext);

        problem.Type.ShouldBe(ProblemTypeUris.NotImplemented);
        problem.Status.ShouldBe(StatusCodes.Status501NotImplemented);
        GetExtensionString(problem, GatewayProblemDetailsExtensions.ReasonCode)
            .ShouldBe(QueryProblemReasonCodes.NotImplemented);
    }

    [Fact]
    public async Task QueryExecutionFailedExceptionHandler_InvalidCursor_UsesBadRequestProblemTypeAndDoesNotEchoCursor() {
        var handler = new QueryExecutionFailedExceptionHandler(NullLogger<QueryExecutionFailedExceptionHandler>.Instance);
        DefaultHttpContext httpContext = CreateHttpContext();
        const string RawCursor = "protected.cursor.payload";
        var exception = new QueryExecutionFailedException(
            "corr-1",
            "tenant-a",
            "party",
            "party-1",
            "SearchParties",
            StatusCodes.Status400BadRequest,
            "invalid-cursor: " + RawCursor,
            QueryProblemReasonCodes.InvalidPage);

        _ = await handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        ProblemDetails problem = await ReadProblemDetailsAsync(httpContext);

        problem.Type.ShouldBe(ProblemTypeUris.BadRequest);
        problem.Status.ShouldBe(StatusCodes.Status400BadRequest);
        problem.Detail.ShouldBe("The supplied cursor is invalid.");
        problem.Detail.ShouldNotContain(RawCursor);
        GetExtensionString(problem, GatewayProblemDetailsExtensions.ReasonCode)
            .ShouldBe(QueryProblemReasonCodes.InvalidPage);
        GetExtensionString(problem, GatewayProblemDetailsExtensions.Reason)
            .ShouldBe(QueryAdapterFailureReason.InvalidCursor);
    }

    /// <summary>
    /// Verifies explicit and fallback paging categories produce safe HTTP responses.
    /// </summary>
    /// <param name="adapterReason">The optional adapter failure discriminator.</param>
    /// <param name="expectedReason">The expected public reason.</param>
    /// <param name="expectedDetail">The expected safe detail.</param>
    /// <returns>A task that represents the verification.</returns>
    [Theory]
    [InlineData(QueryAdapterFailureReason.InvalidPage, "invalid-page", "The supplied page is invalid.")]
    [InlineData(QueryAdapterFailureReason.InvalidCursor, "invalid-cursor", "The supplied cursor is invalid.")]
    [InlineData("unknown-category", "invalid-cursor", "The supplied cursor is invalid.")]
    public async Task QueryExecutionFailedExceptionHandler_PaginationDiscriminator_UsesSafe400(
        string adapterReason,
        string expectedReason,
        string expectedDetail)
    {
        var handler = new QueryExecutionFailedExceptionHandler(NullLogger<QueryExecutionFailedExceptionHandler>.Instance);
        DefaultHttpContext context = CreateHttpContext();
        var exception = new QueryExecutionFailedException(
            "corr-1", "tenant-a", "party", "party-1", "SearchParties",
            StatusCodes.Status400BadRequest,
            "protected.cursor.payload not found not implemented",
            QueryProblemReasonCodes.InvalidPage)
        {
            AdapterFailureReason = adapterReason,
        };

        (await handler.TryHandleAsync(context, exception, CancellationToken.None)).ShouldBeTrue();

        ProblemDetails problem = await ReadProblemDetailsAsync(context);
        context.Response.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        problem.Type.ShouldBe(ProblemTypeUris.BadRequest);
        problem.Status.ShouldBe(StatusCodes.Status400BadRequest);
        problem.Detail.ShouldBe(expectedDetail);
        GetExtensionString(problem, GatewayProblemDetailsExtensions.ReasonCode)
            .ShouldBe(QueryProblemReasonCodes.InvalidPage);
        GetExtensionString(problem, GatewayProblemDetailsExtensions.Reason).ShouldBe(expectedReason);
        JsonSerializer.Serialize(problem).ShouldNotContain("protected.cursor.payload");
    }

    /// <summary>
    /// Verifies existing exception callers do not need an adapter discriminator.
    /// </summary>
    [Fact]
    public void QueryExecutionFailedException_ExistingConstructor_DefaultsDiscriminatorToNull()
    {
        var exception = new QueryExecutionFailedException(
            "corr-1", "tenant-a", "party", "party-1", "SearchParties", 400,
            "legacy detail", QueryProblemReasonCodes.InvalidPage);

        exception.AdapterFailureReason.ShouldBeNull();
        exception.Detail.ShouldBe("legacy detail");
    }

    [Fact]
    public async Task QueryExecutionFailedExceptionHandler_Forbidden_IncludesInsufficientPermissionReasonCode() {
        var handler = new QueryExecutionFailedExceptionHandler(NullLogger<QueryExecutionFailedExceptionHandler>.Instance);
        DefaultHttpContext httpContext = CreateHttpContext();
        var exception = new QueryExecutionFailedException(
            "corr-1",
            "tenant-a",
            "party",
            "party-1",
            "GetParty",
            StatusCodes.Status403Forbidden,
            "Forbidden",
            AuthorizationFailureReasonExtensions.InsufficientPermission);

        _ = await handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        ProblemDetails problem = await ReadProblemDetailsAsync(httpContext);

        problem.Type.ShouldBe(ProblemTypeUris.Forbidden);
        problem.Status.ShouldBe(StatusCodes.Status403Forbidden);
        GetExtensionString(problem, GatewayProblemDetailsExtensions.ReasonCode)
            .ShouldBe(AuthorizationFailureReasonExtensions.InsufficientPermission);
    }

    [Fact]
    public async Task QueryExecutionFailedExceptionHandler_ForbiddenWithoutExplicitReasonCode_FallsBackToInsufficientPermission() {
        var handler = new QueryExecutionFailedExceptionHandler(NullLogger<QueryExecutionFailedExceptionHandler>.Instance);
        DefaultHttpContext httpContext = CreateHttpContext();
        var exception = new QueryExecutionFailedException(
            "corr-1",
            "tenant-a",
            "party",
            "party-1",
            "GetParty",
            StatusCodes.Status403Forbidden,
            "Forbidden");

        _ = await handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        ProblemDetails problem = await ReadProblemDetailsAsync(httpContext);

        problem.Type.ShouldBe(ProblemTypeUris.Forbidden);
        problem.Status.ShouldBe(StatusCodes.Status403Forbidden);
        GetExtensionString(problem, GatewayProblemDetailsExtensions.ReasonCode)
            .ShouldBe(AuthorizationFailureReasonExtensions.InsufficientPermission);
    }

    private static DefaultHttpContext CreateHttpContext() {
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();
        httpContext.Items[CorrelationIdMiddleware.HttpContextKey] = "corr-1";
        httpContext.Request.Path = "/api/v1/queries";
        return httpContext;
    }

    private static async Task<ProblemDetails> ReadProblemDetailsAsync(DefaultHttpContext httpContext) {
        _ = httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        ProblemDetails? problem = await JsonSerializer
            .DeserializeAsync<ProblemDetails>(httpContext.Response.Body)
            .ConfigureAwait(false);
        return problem.ShouldNotBeNull();
    }

    private static string? GetExtensionString(ProblemDetails problem, string extensionName) {
        problem.Extensions.TryGetValue(extensionName, out object? value).ShouldBeTrue();
        return value switch {
            JsonElement { ValueKind: JsonValueKind.String } element => element.GetString(),
            _ => value?.ToString(),
        };
    }
}
