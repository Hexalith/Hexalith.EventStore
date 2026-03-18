
using System.Text.Json;

using Hexalith.EventStore.CommandApi.Middleware;
using Hexalith.EventStore.Server.Queries;

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Hexalith.EventStore.CommandApi.ErrorHandling;

public class QueryExecutionFailedExceptionHandler(ILogger<QueryExecutionFailedExceptionHandler> logger) : IExceptionHandler
{
    private const string ProblemJsonContentType = "application/problem+json";

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(exception);

        if (exception is not QueryExecutionFailedException queryFailure)
        {
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

        string detail = queryFailure.StatusCode == StatusCodes.Status403Forbidden
            ? AuthorizationExceptionHandler.SanitizeForbiddenTerms(queryFailure.Detail)
            : queryFailure.Detail;

        var problemDetails = new ProblemDetails
        {
            Status = queryFailure.StatusCode,
            Title = GetTitle(queryFailure.StatusCode),
            Type = GetProblemTypeUri(queryFailure.StatusCode),
            Detail = detail,
            Instance = httpContext.Request.Path,
            Extensions =
            {
                ["correlationId"] = correlationId,
            },
        };

        if (queryFailure.StatusCode == StatusCodes.Status403Forbidden)
        {
            problemDetails.Extensions["tenantId"] = queryFailure.Tenant;
        }

        httpContext.Response.StatusCode = queryFailure.StatusCode;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, (JsonSerializerOptions?)null, ProblemJsonContentType, cancellationToken).ConfigureAwait(false);
        return true;
    }

    private static string GetTitle(int statusCode)
        => statusCode switch
        {
            StatusCodes.Status403Forbidden => "Forbidden",
            StatusCodes.Status501NotImplemented => "Not Implemented",
            _ => "Query Failed",
        };

    private static string GetProblemTypeUri(int statusCode)
        => statusCode switch
        {
            StatusCodes.Status403Forbidden => ProblemTypeUris.Forbidden,
            StatusCodes.Status501NotImplemented => ProblemTypeUris.NotImplemented,
            _ => ProblemTypeUris.InternalServerError,
        };
}
