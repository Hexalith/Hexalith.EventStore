
using System.Text.Json;

using Hexalith.EventStore.Contracts.Authorization;
using Hexalith.EventStore.Contracts.Problems;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Middleware;
using Hexalith.EventStore.Server.Queries;

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Hexalith.EventStore.ErrorHandling;

public class QueryExecutionFailedExceptionHandler(ILogger<QueryExecutionFailedExceptionHandler> logger) : IExceptionHandler {
    private const string ProblemJsonContentType = "application/problem+json";

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(exception);

        if (exception is not QueryExecutionFailedException queryFailure) {
            return false;
        }

        string correlationId = httpContext.Items[CorrelationIdMiddleware.HttpContextKey]?.ToString()
            ?? queryFailure.CorrelationId;

        logger.LogWarning(
            "Query failed with mapped HTTP status: CorrelationId={CorrelationId}, Tenant={Tenant}, Domain={Domain}, AggregateId={AggregateId}, QueryType={QueryType}, StatusCode={StatusCode}",
            correlationId,
            queryFailure.Tenant,
            queryFailure.Domain,
            queryFailure.AggregateId,
            queryFailure.QueryType,
            queryFailure.StatusCode);

        string detail = GetSafeDetail(queryFailure);

        var problemDetails = new ProblemDetails {
            Status = queryFailure.StatusCode,
            Title = GetTitle(queryFailure.StatusCode),
            Type = GetProblemTypeUri(queryFailure.StatusCode),
            Detail = detail,
            Instance = httpContext.Request.Path,
            Extensions =
            {
                [GatewayProblemDetailsExtensions.CorrelationId] = correlationId,
                [GatewayProblemDetailsExtensions.ReasonCode] = queryFailure.ReasonCode
                    ?? GetReasonCode(queryFailure.StatusCode),
            },
        };

        if (queryFailure.StatusCode == StatusCodes.Status500InternalServerError) {
            problemDetails.Extensions[GatewayProblemDetailsExtensions.RetryAfter] = "30";
            problemDetails.Extensions[GatewayProblemDetailsExtensions.Degradation] = "projection-query-unavailable";
            problemDetails.Extensions[GatewayProblemDetailsExtensions.Reason] =
                "Projection query could not be completed safely. Retry after the advised interval.";
            httpContext.Response.Headers.RetryAfter = "30";
        }

        if (queryFailure.StatusCode == StatusCodes.Status403Forbidden) {
            problemDetails.Extensions[GatewayProblemDetailsExtensions.TenantId] = queryFailure.Tenant;
        }

        httpContext.Response.StatusCode = queryFailure.StatusCode;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, (JsonSerializerOptions?)null, ProblemJsonContentType, cancellationToken).ConfigureAwait(false);
        return true;
    }

    private static string GetTitle(int statusCode)
        => statusCode switch {
            StatusCodes.Status400BadRequest => "Bad Request",
            StatusCodes.Status403Forbidden => "Forbidden",
            StatusCodes.Status501NotImplemented => "Not Implemented",
            _ => "Query Failed",
        };

    private static string GetSafeDetail(QueryExecutionFailedException queryFailure)
        => queryFailure.StatusCode switch {
            StatusCodes.Status400BadRequest when string.Equals(queryFailure.ReasonCode, QueryProblemReasonCodes.InvalidPage, StringComparison.Ordinal) =>
                "The supplied cursor is invalid.",
            StatusCodes.Status403Forbidden => AuthorizationExceptionHandler.SanitizeForbiddenTerms(queryFailure.Detail),
            _ => queryFailure.Detail,
        };

    private static string GetProblemTypeUri(int statusCode)
        => statusCode switch {
            StatusCodes.Status400BadRequest => ProblemTypeUris.BadRequest,
            StatusCodes.Status403Forbidden => ProblemTypeUris.Forbidden,
            StatusCodes.Status501NotImplemented => ProblemTypeUris.NotImplemented,
            _ => ProblemTypeUris.InternalServerError,
        };

    private static string GetReasonCode(int statusCode)
        => statusCode switch {
            StatusCodes.Status400BadRequest => QueryProblemReasonCodes.MalformedRequest,
            StatusCodes.Status403Forbidden => AuthorizationFailureReasonExtensions.InsufficientPermission,
            StatusCodes.Status501NotImplemented => QueryProblemReasonCodes.NotImplemented,
            _ => QueryProblemReasonCodes.InternalError,
        };
}
