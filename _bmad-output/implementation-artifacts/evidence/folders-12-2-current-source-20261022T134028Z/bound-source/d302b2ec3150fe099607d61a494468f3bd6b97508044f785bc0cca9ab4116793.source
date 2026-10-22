using System.Text.Json;

using Hexalith.EventStore.Contracts.Problems;
using Hexalith.EventStore.Middleware;
using Hexalith.EventStore.Server.Commands;

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Hexalith.EventStore.ErrorHandling;

/// <summary>Maps canonical live intent conflicts to a support-safe HTTP 409 response.</summary>
public sealed class IdempotencyConflictExceptionHandler(
    ILogger<IdempotencyConflictExceptionHandler> logger) : IExceptionHandler
{
    /// <inheritdoc/>
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(exception);
        IdempotencyConflictException? conflict = exception as IdempotencyConflictException
            ?? exception.InnerException as IdempotencyConflictException;
        if (conflict is null)
        {
            return false;
        }

        string correlationId = httpContext.Items[CorrelationIdMiddleware.HttpContextKey]?.ToString()
            ?? conflict.CorrelationId;
        logger.LogInformation(
            "Canonical idempotency intent conflict. CorrelationId={CorrelationId}, Stage=IdempotencyConflict",
            correlationId);
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "Idempotency Conflict",
            Type = ProblemTypeUris.IdempotencyConflict,
            Detail = "The idempotency key cannot be used for this mutation. Use the original intent or submit with a new key.",
            Instance = httpContext.Request.Path,
            Extensions =
            {
                [GatewayProblemDetailsExtensions.CorrelationId] = correlationId,
                [GatewayProblemDetailsExtensions.ReasonCode] = "idempotency_conflict",
                [GatewayProblemDetailsExtensions.Code] = "idempotency_conflict",
                [GatewayProblemDetailsExtensions.Category] = "idempotency_conflict",
                [GatewayProblemDetailsExtensions.Retryable] = false,
            },
        };

        httpContext.Response.StatusCode = StatusCodes.Status409Conflict;
        httpContext.Response.Headers.Remove("Retry-After");
        await httpContext.Response.WriteAsJsonAsync(
            problem,
            (JsonSerializerOptions?)null,
            "application/problem+json",
            CancellationToken.None).ConfigureAwait(false);
        return true;
    }
}
