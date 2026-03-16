
using System.Text.Json;

using Hexalith.EventStore.Server.Queries;

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Hexalith.EventStore.CommandApi.ErrorHandling;

public class QueryNotFoundExceptionHandler(ILogger<QueryNotFoundExceptionHandler> logger) : IExceptionHandler {
    private const string ProblemJsonContentType = "application/problem+json";

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(httpContext);

        if (exception is not QueryNotFoundException) {
            return false;
        }

        string correlationId = httpContext.Items["CorrelationId"]?.ToString() ?? "unknown";

        logger.LogWarning(
            "Query not found: CorrelationId={CorrelationId}",
            correlationId);

        // SECURITY: Do NOT include tenant, domain, aggregateId, or actor ID in response body
        var problemDetails = new ProblemDetails {
            Status = StatusCodes.Status404NotFound,
            Title = "Not Found",
            Type = ProblemTypeUris.NotFound,
            Detail = "No projection found for the requested resource.",
            Instance = httpContext.Request.Path,
            Extensions = { ["correlationId"] = correlationId },
        };

        httpContext.Response.StatusCode = StatusCodes.Status404NotFound;

        await httpContext.Response.WriteAsJsonAsync(problemDetails, (JsonSerializerOptions?)null, ProblemJsonContentType, cancellationToken).ConfigureAwait(false);

        return true;
    }
}
