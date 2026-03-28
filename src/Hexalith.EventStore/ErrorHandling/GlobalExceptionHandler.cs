
using Hexalith.EventStore.Middleware;

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Hexalith.EventStore.ErrorHandling;

public class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler {
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(exception);

        string correlationId = httpContext.Items[CorrelationIdMiddleware.HttpContextKey]?.ToString() ?? "unknown";

        logger.LogError(exception, "Unhandled exception. CorrelationId={CorrelationId}", correlationId);

        string? tenantId = httpContext.Items.TryGetValue("RequestTenantId", out object? tenantObj) && tenantObj is string t && !string.IsNullOrEmpty(t) ? t : null;

        var problemDetails = new ProblemDetails {
            Status = StatusCodes.Status500InternalServerError,
            Title = "Internal Server Error",
            Type = ProblemTypeUris.InternalServerError,
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
            CancellationToken.None).ConfigureAwait(false);

        return true;
    }
}
