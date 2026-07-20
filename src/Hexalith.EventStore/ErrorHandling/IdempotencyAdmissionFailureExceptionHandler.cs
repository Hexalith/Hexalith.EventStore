using System.Text.Json;

using Hexalith.EventStore.Contracts.Problems;
using Hexalith.EventStore.Middleware;
using Hexalith.EventStore.Server.Commands;

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Hexalith.EventStore.ErrorHandling;

/// <summary>Maps fail-closed admission outcomes to stable support-safe Problem Details.</summary>
public sealed class IdempotencyAdmissionFailureExceptionHandler(
    ILogger<IdempotencyAdmissionFailureExceptionHandler> logger) : IExceptionHandler
{
    /// <inheritdoc/>
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(exception);
        IdempotencyAdmissionFailureException? failure = exception as IdempotencyAdmissionFailureException
            ?? exception.InnerException as IdempotencyAdmissionFailureException;
        if (failure is null)
        {
            return false;
        }

        string correlationId = httpContext.Items[CorrelationIdMiddleware.HttpContextKey]?.ToString()
            ?? failure.CorrelationId;
        logger.LogWarning(
            "Idempotency admission failed closed. Code={Code}, CorrelationId={CorrelationId}, Stage=IdempotencyAdmissionFailedClosed",
            failure.Code,
            correlationId);
        var problem = new ProblemDetails
        {
            Status = failure.StatusCode,
            Title = "Idempotency Admission Failed",
            Type = ProblemTypeUris.IdempotencyAdmissionFailure,
            Detail = failure.Message,
            Instance = httpContext.Request.Path,
            Extensions =
            {
                [GatewayProblemDetailsExtensions.CorrelationId] = correlationId,
                [GatewayProblemDetailsExtensions.ReasonCode] = failure.Code,
                [GatewayProblemDetailsExtensions.Code] = failure.Code,
                [GatewayProblemDetailsExtensions.Category] = failure.Category,
                [GatewayProblemDetailsExtensions.Retryable] = failure.Retryable,
                [GatewayProblemDetailsExtensions.ClientAction] = failure.ClientAction,
            },
        };

        httpContext.Response.StatusCode = failure.StatusCode;
        if (failure.Retryable)
        {
            httpContext.Response.Headers.RetryAfter = "1";
        }
        else
        {
            httpContext.Response.Headers.Remove("Retry-After");
        }

        await httpContext.Response.WriteAsJsonAsync(
            problem,
            (JsonSerializerOptions?)null,
            "application/problem+json",
            CancellationToken.None).ConfigureAwait(false);
        return true;
    }
}
