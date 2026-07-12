using System.Text.Json;

using Hexalith.EventStore.Middleware;
using Hexalith.EventStore.Server.Commands;

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Hexalith.EventStore.ErrorHandling;

/// <summary>Maps fail-closed command identity collisions to a non-retryable HTTP 409 response.</summary>
public sealed class CommandIdentityConflictExceptionHandler(
    ILogger<CommandIdentityConflictExceptionHandler> logger) : IExceptionHandler
{
    /// <inheritdoc/>
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(exception);

        CommandIdentityConflictException? conflict = Find(exception, 10);
        if (conflict is null)
        {
            return false;
        }

        string correlationId = httpContext.Items[CorrelationIdMiddleware.HttpContextKey]?.ToString()
            ?? conflict.CorrelationId;

        logger.LogWarning(
            "Command identity conflict: MessageId={MessageId}, CorrelationId={CorrelationId}, TenantId={TenantId}",
            conflict.MessageId,
            correlationId,
            conflict.TenantId);

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "Command Identity Conflict",
            Type = ProblemTypeUris.CommandIdentityConflict,
            Detail = "The supplied MessageId cannot be verified for this command. Submit the original command identity tuple or use a new MessageId.",
            Instance = httpContext.Request.Path,
            Extensions =
            {
                ["correlationId"] = correlationId,
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

    private static CommandIdentityConflictException? Find(Exception? exception, int remainingDepth)
    {
        if (exception is null || remainingDepth <= 0)
        {
            return null;
        }

        if (exception is CommandIdentityConflictException conflict)
        {
            return conflict;
        }

        if (exception is AggregateException aggregate)
        {
            foreach (Exception inner in aggregate.InnerExceptions)
            {
                CommandIdentityConflictException? found = Find(inner, remainingDepth - 1);
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
