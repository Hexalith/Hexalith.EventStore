
using System.Text.Json;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Middleware;
using Hexalith.EventStore.Server.Commands;

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Hexalith.EventStore.ErrorHandling;

public class ConcurrencyConflictExceptionHandler(
    ICommandStatusStore statusStore,
    ILogger<ConcurrencyConflictExceptionHandler> logger) : IExceptionHandler {
    private const string ProblemJsonContentType = "application/problem+json";

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(exception);

        // Unwrap InnerException chain -- DAPR actor proxy may wrap exceptions
        // in ActorMethodInvocationException. Check the full chain for ConcurrencyConflictException.
        ConcurrencyConflictException? conflict = FindConcurrencyConflict(exception);
        if (conflict is null) {
            return false;
        }

        // Prefer HTTP request correlation ID for API-level tracing (enables end-to-end correlation
        // from client request through the full pipeline). The exception's correlation ID is used as
        // fallback for actor-generated conflicts where HTTP context may be unavailable.
        string correlationId = httpContext.Items[CorrelationIdMiddleware.HttpContextKey]?.ToString()
            ?? conflict.CorrelationId;

        logger.LogWarning(
            "Concurrency conflict: CorrelationId={CorrelationId}, AggregateId={AggregateId}, TenantId={TenantId}",
            correlationId,
            conflict.AggregateId,
            conflict.TenantId);

        // Advisory status write: Rejected with ConcurrencyConflict reason (rule #12)
        // Note: If the request is cancelled before we start the status write, we want to abort.
        // But if cancellation occurs DURING the status write, we still send the 409 response
        // (advisory writes must not block the error response).
        if (!cancellationToken.IsCancellationRequested) {
            try {
                if (conflict.TenantId is not null) {
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
            catch (OperationCanceledException) {
                // Cancellation during status write - log but don't block 409 response
                logger.LogWarning(
                    "Status write cancelled during concurrency conflict handling. CorrelationId={CorrelationId}",
                    correlationId);
            }
            catch (Exception ex) {
                logger.LogWarning(
                    ex,
                    "Failed to write Rejected status for concurrency conflict. CorrelationId={CorrelationId}, AggregateId={AggregateId}, TenantId={TenantId}",
                    correlationId,
                    conflict.AggregateId,
                    conflict.TenantId);
            }
        }

        // UX-DR10: No aggregateId, conflictSource, or tenantId in client response
        // UX-DR6: No event sourcing terminology — use safe, generic detail message
        var problemDetails = new ProblemDetails {
            Status = StatusCodes.Status409Conflict,
            Title = "Conflict",
            Type = ProblemTypeUris.ConcurrencyConflict,
            Detail = "A concurrency conflict occurred. Please retry the command.",
            Instance = httpContext.Request.Path,
            Extensions =
            {
                ["correlationId"] = correlationId,
            },
        };

        httpContext.Response.StatusCode = StatusCodes.Status409Conflict;
        // Add Retry-After header to help consumers pace retries and avoid thundering herd
        httpContext.Response.Headers["Retry-After"] = "1";
        // Use CancellationToken.None to ensure the full 409 ProblemDetails response is always
        // written completely. Unlike advisory status writes, this is the authoritative error
        // response -- a partial/truncated body would leave the client with a malformed response.
        await httpContext.Response.WriteAsJsonAsync(problemDetails, (JsonSerializerOptions?)null, ProblemJsonContentType, CancellationToken.None)
            .ConfigureAwait(false);

        return true;
    }

    /// <summary>
    /// Walks the InnerException chain looking for ConcurrencyConflictException.
    /// DAPR actor proxy wraps actor-thrown exceptions in ActorMethodInvocationException,
    /// so the handler must unwrap to find the real cause. Also traverses
    /// AggregateException.InnerExceptions collections for task-based scenarios.
    /// Limits depth to 10 to prevent infinite loops on circular exception references.
    /// </summary>
    private static ConcurrencyConflictException? FindConcurrencyConflict(Exception? exception) {
        const int maxDepth = 10;
        return FindConcurrencyConflictRecursive(exception, maxDepth);
    }

    private static ConcurrencyConflictException? FindConcurrencyConflictRecursive(Exception? exception, int remainingDepth) {
        if (exception is null || remainingDepth <= 0) {
            return null;
        }

        if (exception is ConcurrencyConflictException conflict) {
            return conflict;
        }

        // AggregateException has multiple InnerExceptions -- search all of them
        if (exception is AggregateException aggregate) {
            foreach (Exception inner in aggregate.InnerExceptions) {
                ConcurrencyConflictException? found = FindConcurrencyConflictRecursive(inner, remainingDepth - 1);
                if (found is not null) {
                    return found;
                }
            }

            return null;
        }

        return FindConcurrencyConflictRecursive(exception.InnerException, remainingDepth - 1);
    }
}
