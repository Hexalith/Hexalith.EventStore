using System.Text.Json;

using Hexalith.EventStore.Contracts.Problems;
using Hexalith.EventStore.Middleware;
using Hexalith.EventStore.Server.Commands;

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Hexalith.EventStore.ErrorHandling;

/// <summary>Maps consumed-key expiry to the stable non-retryable HTTP 409 contract.</summary>
public sealed class IdempotencyKeyExpiredExceptionHandler(
    ILogger<IdempotencyKeyExpiredExceptionHandler> logger) : IExceptionHandler
{
    /// <inheritdoc/>
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(exception);

        IdempotencyKeyExpiredException? expired = Find(exception, 10);
        if (expired is null)
        {
            return false;
        }

        string correlationId = httpContext.Items[CorrelationIdMiddleware.HttpContextKey]?.ToString()
            ?? expired.CorrelationId;

        logger.LogInformation(
            "Consumed idempotency key replay result expired. CorrelationId={CorrelationId}, Stage=IdempotencyKeyExpired",
            correlationId);

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "Idempotency Key Expired",
            Type = ProblemTypeUris.IdempotencyKeyExpired,
            Detail = "Refresh current state, then submit the intended mutation with a new idempotency key.",
            Instance = httpContext.Request.Path,
            Extensions =
            {
                [GatewayProblemDetailsExtensions.CorrelationId] = correlationId,
                [GatewayProblemDetailsExtensions.ReasonCode] = "idempotency_key_expired",
                [GatewayProblemDetailsExtensions.Code] = "idempotency_key_expired",
                [GatewayProblemDetailsExtensions.Category] = "idempotency_key_expired",
                [GatewayProblemDetailsExtensions.Retryable] = false,
                [GatewayProblemDetailsExtensions.ClientAction] = "refresh_state_then_submit_with_new_key",
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

    private static IdempotencyKeyExpiredException? Find(Exception? exception, int remainingDepth)
    {
        if (exception is null || remainingDepth <= 0)
        {
            return null;
        }

        if (exception is IdempotencyKeyExpiredException expired)
        {
            return expired;
        }

        if (exception is AggregateException aggregate)
        {
            foreach (Exception inner in aggregate.InnerExceptions)
            {
                IdempotencyKeyExpiredException? found = Find(inner, remainingDepth - 1);
                if (found is not null)
                {
                    return found;
                }
            }

            return null;
        }

        return Find(exception.InnerException, remainingDepth - 1);
    }
}
