
using System.Text.Json;

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Hexalith.EventStore.CommandApi.ErrorHandling;

/// <summary>
/// Handles <see cref="AuthorizationServiceUnavailableException"/> by returning 503 Service Unavailable
/// with a Retry-After header and RFC 9457 ProblemDetails body.
/// SECURITY: The response body contains a generic message only — no actor type, actor ID, or internal details.
/// </summary>
public class AuthorizationServiceUnavailableHandler(
    ILogger<AuthorizationServiceUnavailableHandler> logger) : IExceptionHandler {
    private const string ProblemJsonContentType = "application/problem+json";

    /// <inheritdoc/>
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(httpContext);

        if (exception is not AuthorizationServiceUnavailableException unavailable) {
            return false;
        }

        string correlationId = httpContext.Items["CorrelationId"]?.ToString() ?? "unknown";

        // Log at Error level with FULL internal details (server-side only)
        logger.LogError(
            exception,
            "Authorization service unavailable: SecurityEvent={SecurityEvent}, CorrelationId={CorrelationId}, ActorType={ActorType}, ActorId={ActorId}, Reason={Reason}",
            "AuthorizationServiceUnavailable",
            correlationId,
            unavailable.ActorTypeName,
            unavailable.ActorId,
            unavailable.Reason);

        httpContext.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        httpContext.Response.Headers.RetryAfter = "30"; // UX-DR5: fixed 30s for 503

        // SECURITY: Generic message only — no actor type, actor ID, or internal details
        // UX-DR11: "command processing pipeline" — never name internal components
        // UX-DR2: No correlationId on 503 (pre-pipeline rejection)
        var problemDetails = new ProblemDetails {
            Status = StatusCodes.Status503ServiceUnavailable,
            Title = "Service Unavailable",
            Type = ProblemTypeUris.ServiceUnavailable,
            Detail = "The command processing pipeline is temporarily unavailable. Please retry after the specified interval.",
            Instance = httpContext.Request.Path,
        };

        // Use CancellationToken.None to ensure the full ProblemDetails response is always written
        await httpContext.Response.WriteAsJsonAsync(problemDetails, (JsonSerializerOptions?)null, ProblemJsonContentType, CancellationToken.None).ConfigureAwait(false);

        return true;
    }
}
