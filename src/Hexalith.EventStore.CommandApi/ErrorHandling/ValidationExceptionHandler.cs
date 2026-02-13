namespace Hexalith.EventStore.CommandApi.ErrorHandling;

using FluentValidation;

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Handles FluentValidation exceptions and converts them to RFC 7807 ProblemDetails.
/// </summary>
public class ValidationExceptionHandler(ILogger<ValidationExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        if (exception is not ValidationException validationException)
        {
            return false;
        }

        string correlationId = httpContext.Items["CorrelationId"]?.ToString() ?? "unknown";

        logger.LogWarning(
            validationException,
            "Validation failed. CorrelationId={CorrelationId}, Errors={ErrorCount}",
            correlationId,
            validationException.Errors.Count());

        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Validation Failed",
            Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
            Detail = "One or more validation errors occurred.",
            Extensions =
            {
                ["correlationId"] = correlationId,
                ["validationErrors"] = validationException.Errors.Select(e => new
                {
                    field = e.PropertyName,
                    message = e.ErrorMessage,
                }).ToArray(),
            },
        };

        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken).ConfigureAwait(false);

        return true;
    }
}
