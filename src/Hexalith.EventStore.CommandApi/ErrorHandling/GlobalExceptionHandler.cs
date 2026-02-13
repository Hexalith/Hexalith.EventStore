namespace Hexalith.EventStore.CommandApi.ErrorHandling;

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

public class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        string correlationId = httpContext.Items["CorrelationId"]?.ToString() ?? "unknown";

        logger.LogError(exception, "Unhandled exception. CorrelationId={CorrelationId}", correlationId);

        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "Internal Server Error",
            Type = "https://tools.ietf.org/html/rfc7231#section-6.6.1",
            Detail = "An unexpected error occurred while processing your request.",
            Extensions = { ["correlationId"] = correlationId },
        };

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken).ConfigureAwait(false);

        return true;
    }
}
