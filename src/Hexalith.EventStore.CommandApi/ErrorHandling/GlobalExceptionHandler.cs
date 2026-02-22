namespace Hexalith.EventStore.CommandApi.ErrorHandling;

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

public class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler {
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(httpContext);

        string correlationId = httpContext.Items["CorrelationId"]?.ToString() ?? "unknown";

        logger.LogError(exception, "Unhandled exception. CorrelationId={CorrelationId}", correlationId);

        string? tenantId = httpContext.Items.TryGetValue("RequestTenantId", out object? tenantObj) && tenantObj is string t && !string.IsNullOrEmpty(t) ? t : null;

        var problemDetails = new ProblemDetails {
            Status = StatusCodes.Status500InternalServerError,
            Title = "Internal Server Error",
            Type = "https://tools.ietf.org/html/rfc9457#section-3",
            Detail = "An unexpected error occurred while processing your request.",
            Instance = httpContext.Request.Path,
            Extensions = { ["correlationId"] = correlationId },
        };

        if (tenantId is not null) {
            problemDetails.Extensions["tenantId"] = tenantId;
        }

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await httpContext.Response.WriteAsJsonAsync(
            problemDetails,
            (System.Text.Json.JsonSerializerOptions?)null,
            "application/problem+json",
            cancellationToken).ConfigureAwait(false);

        return true;
    }
}
