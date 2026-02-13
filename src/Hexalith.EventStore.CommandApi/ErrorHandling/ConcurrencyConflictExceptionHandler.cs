namespace Hexalith.EventStore.CommandApi.ErrorHandling;

using System.Text.Json;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Server.Commands;

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

public class ConcurrencyConflictExceptionHandler(
    ICommandStatusStore statusStore,
    ILogger<ConcurrencyConflictExceptionHandler> logger) : IExceptionHandler
{
    private const string ProblemJsonContentType = "application/problem+json";

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        // Unwrap InnerException chain -- DAPR actor proxy may wrap exceptions
        // in ActorMethodInvocationException. Check the full chain for ConcurrencyConflictException.
        ConcurrencyConflictException? conflict = FindConcurrencyConflict(exception);
        if (conflict is null)
        {
            return false;
        }

        string correlationId = httpContext.Items["CorrelationId"]?.ToString()
            ?? conflict.CorrelationId;

        logger.LogWarning(
            "Concurrency conflict: CorrelationId={CorrelationId}, AggregateId={AggregateId}, TenantId={TenantId}",
            correlationId,
            conflict.AggregateId,
            conflict.TenantId);

        // Advisory status write: Rejected with ConcurrencyConflict reason (rule #12)
        try
        {
            if (conflict.TenantId is not null)
            {
                await statusStore.WriteStatusAsync(
                    conflict.TenantId,
                    conflict.CorrelationId,
                    new CommandStatusRecord(
                        CommandStatus.Rejected,
                        DateTimeOffset.UtcNow,
                        conflict.AggregateId,
                        EventCount: null,
                        RejectionEventType: null,
                        FailureReason: "ConcurrencyConflict",
                        TimeoutDuration: null),
                    cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Failed to write Rejected status for concurrency conflict. CorrelationId={CorrelationId}",
                correlationId);
        }

        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "Conflict",
            Type = "https://tools.ietf.org/html/rfc9457#section-3",
            Detail = conflict.Message,
            Instance = httpContext.Request.Path,
            Extensions =
            {
                ["correlationId"] = correlationId,
                ["aggregateId"] = conflict.AggregateId,
            },
        };

        string? tenantId = conflict.TenantId
            ?? (httpContext.Items.TryGetValue("RequestTenantId", out var t) && t is string ts
                ? ts : null);

        if (tenantId is not null)
        {
            problemDetails.Extensions["tenantId"] = tenantId;
        }

        httpContext.Response.StatusCode = StatusCodes.Status409Conflict;
        // Add Retry-After header to help consumers pace retries and avoid thundering herd
        httpContext.Response.Headers["Retry-After"] = "1";
        await httpContext.Response.WriteAsJsonAsync(problemDetails, (JsonSerializerOptions?)null, ProblemJsonContentType, cancellationToken)
            .ConfigureAwait(false);

        return true;
    }

    /// <summary>
    /// Walks the InnerException chain looking for ConcurrencyConflictException.
    /// DAPR actor proxy wraps actor-thrown exceptions in ActorMethodInvocationException,
    /// so the handler must unwrap to find the real cause. Limits depth to 10 to prevent
    /// infinite loops on circular exception references.
    /// </summary>
    internal static ConcurrencyConflictException? FindConcurrencyConflict(Exception? exception)
    {
        const int maxDepth = 10;
        Exception? current = exception;
        for (int i = 0; i < maxDepth && current is not null; i++)
        {
            if (current is ConcurrencyConflictException conflict)
            {
                return conflict;
            }

            current = current.InnerException;
        }

        return null;
    }
}
