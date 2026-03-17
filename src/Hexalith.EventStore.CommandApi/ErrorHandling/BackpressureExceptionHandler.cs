
using System.Text.Json;

using Hexalith.EventStore.CommandApi.Middleware;
using Hexalith.EventStore.Server.Commands;

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Hexalith.EventStore.CommandApi.ErrorHandling;

/// <summary>
/// Handles <see cref="BackpressureExceededException"/> by returning HTTP 429 with ProblemDetails (FR67).
/// Unlike <see cref="ConcurrencyConflictExceptionHandler"/>, this handler does NOT:
/// - Unwrap DAPR exceptions (backpressure is thrown from MediatR pipeline, not from actor)
/// - Write advisory CommandStatusRecord (the command was never accepted into the pipeline)
/// Security: Does NOT expose aggregateId, actorId, tenantId, or currentDepth in the response (UX-DR10, Rule E6).
/// </summary>
public class BackpressureExceptionHandler(
    ILogger<BackpressureExceptionHandler> logger) : IExceptionHandler
{
    private const string ProblemJsonContentType = "application/problem+json";

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(exception);

        // No DAPR unwrap needed — this exception is thrown directly from SubmitCommandHandler
        if (exception is not BackpressureExceededException backpressure)
        {
            return false;
        }

        // Prefer HTTP request correlation ID for API-level tracing
        string correlationId = httpContext.Items[CorrelationIdMiddleware.HttpContextKey]?.ToString()
            ?? backpressure.CorrelationId;

        logger.LogWarning(
            "Backpressure exceeded: CorrelationId={CorrelationId}, ActorId={ActorId}, TenantId={TenantId}, CurrentDepth={CurrentDepth}",
            correlationId,
            backpressure.AggregateActorId,
            backpressure.TenantId,
            backpressure.CurrentDepth);

        // UX-DR10: No aggregateId, actorId, tenantId, or currentDepth in client response
        // UX-DR6: No event sourcing terminology — use safe, generic detail message
        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Title = "Too Many Requests",
            Type = ProblemTypeUris.BackpressureExceeded,
            Detail = "Too many pending commands for this entity. Please retry after the specified interval.",
            Instance = httpContext.Request.Path,
            Extensions =
            {
                ["correlationId"] = correlationId,
            },
        };

        httpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        // Short Retry-After — backpressure is transient, in-flight commands complete quickly
        httpContext.Response.Headers["Retry-After"] = "1";
        // Use CancellationToken.None to ensure the full ProblemDetails response is always written completely
        await httpContext.Response.WriteAsJsonAsync(problemDetails, (JsonSerializerOptions?)null, ProblemJsonContentType, CancellationToken.None)
            .ConfigureAwait(false);

        return true;
    }
}
