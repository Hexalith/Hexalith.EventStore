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

        string? tenantId = ExtractTenantId(httpContext);

        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Validation Failed",
            Type = "https://tools.ietf.org/html/rfc9457#section-3",
            Detail = "One or more validation errors occurred.",
            Instance = httpContext.Request.Path,
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

        if (tenantId is not null)
        {
            problemDetails.Extensions["tenantId"] = tenantId;
        }

        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
        await httpContext.Response.WriteAsJsonAsync(
            problemDetails,
            (System.Text.Json.JsonSerializerOptions?)null,
            "application/problem+json",
            cancellationToken).ConfigureAwait(false);

        return true;
    }

    private static string? ExtractTenantId(HttpContext httpContext)
    {
        // Try to extract tenant from validation error context — the property name tells us the request was parsed
        if (httpContext.Items.TryGetValue("RequestTenantId", out object? tenantObj) && tenantObj is string tenant && !string.IsNullOrEmpty(tenant))
        {
            return tenant;
        }

        return null;
    }
}
