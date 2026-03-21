
using FluentValidation;

using Hexalith.EventStore.CommandApi.Middleware;

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Hexalith.EventStore.CommandApi.ErrorHandling;

/// <summary>
/// Handles FluentValidation exceptions and converts them to RFC 7807 ProblemDetails.
/// </summary>
public class ValidationExceptionHandler(ILogger<ValidationExceptionHandler> logger) : IExceptionHandler {
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(httpContext);

        if (exception is not ValidationException validationException) {
            return false;
        }

        string correlationId = httpContext.Items[CorrelationIdMiddleware.HttpContextKey]?.ToString() ?? "unknown";

        logger.LogWarning(
            validationException,
            "Validation failed. CorrelationId={CorrelationId}, Errors={ErrorCount}",
            correlationId,
            validationException.Errors.Count());

        string? tenantId = ExtractTenantId(httpContext);
        int errorCount = validationException.Errors.Count();

        ProblemDetails problemDetails = ValidationProblemDetailsFactory.Create(
            $"The command has {errorCount} validation error(s). See 'errors' for specifics.",
            validationException.Errors,
            correlationId,
            tenantId);
        problemDetails.Instance = httpContext.Request.Path;

        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
        httpContext.Response.ContentType = "application/problem+json";
        await httpContext.Response.WriteAsJsonAsync(
            problemDetails,
            (System.Text.Json.JsonSerializerOptions?)null,
            "application/problem+json",
            cancellationToken).ConfigureAwait(false);

        return true;
    }

    private static string? ExtractTenantId(HttpContext httpContext) {
        // Try to extract tenant from validation error context — the property name tells us the request was parsed
        if (httpContext.Items.TryGetValue("RequestTenantId", out object? tenantObj) && tenantObj is string tenant && !string.IsNullOrEmpty(tenant)) {
            return tenant;
        }

        return null;
    }
}
