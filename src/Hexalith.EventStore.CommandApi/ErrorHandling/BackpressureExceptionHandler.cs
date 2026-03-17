using System.Text.Json;

using Hexalith.EventStore.CommandApi.Middleware;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Configuration;

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.CommandApi.ErrorHandling;

/// <summary>
/// Handles <see cref="BackpressureExceededException"/> by returning HTTP 429 Too Many Requests
/// with a Retry-After header and RFC 7807 ProblemDetails body (Story 4.3, FR67).
/// </summary>
public class BackpressureExceptionHandler(
    IOptions<BackpressureOptions> backpressureOptions,
    ILogger<BackpressureExceptionHandler> logger) : IExceptionHandler {
    private const string _problemJsonContentType = "application/problem+json";

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(exception);

        BackpressureExceededException? backpressure = FindBackpressureException(exception);
        if (backpressure is null) {
            return false;
        }

        string correlationId = httpContext.Items[CorrelationIdMiddleware.HttpContextKey]?.ToString()
            ?? backpressure.CorrelationId;

        logger.LogWarning(
            "Backpressure exceeded: CorrelationId={CorrelationId}, TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}",
            correlationId,
            backpressure.TenantId,
            backpressure.Domain,
            backpressure.AggregateId);

        int retryAfterSeconds = backpressureOptions.Value.RetryAfterSeconds;

        var problemDetails = new ProblemDetails {
            Status = StatusCodes.Status429TooManyRequests,
            Title = "Too Many Requests",
            Type = ProblemTypeUris.BackpressureExceeded,
            Detail = "The target aggregate is under backpressure due to excessive pending commands. Please retry after the specified interval.",
            Instance = httpContext.Request.Path,
            Extensions =
            {
                ["correlationId"] = correlationId,
                ["tenantId"] = backpressure.TenantId,
                ["domain"] = backpressure.Domain,
                ["aggregateId"] = backpressure.AggregateId,
            },
        };

        httpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        httpContext.Response.Headers["Retry-After"] = retryAfterSeconds.ToString();
        await httpContext.Response.WriteAsJsonAsync(problemDetails, (JsonSerializerOptions?)null, _problemJsonContentType, CancellationToken.None)
            .ConfigureAwait(false);

        return true;
    }

    private static BackpressureExceededException? FindBackpressureException(Exception? exception) {
        const int maxDepth = 10;
        return FindBackpressureExceptionRecursive(exception, maxDepth);
    }

    private static BackpressureExceededException? FindBackpressureExceptionRecursive(Exception? exception, int remainingDepth) {
        if (exception is null || remainingDepth <= 0) {
            return null;
        }

        if (exception is BackpressureExceededException backpressure) {
            return backpressure;
        }

        if (exception is AggregateException aggregate) {
            foreach (Exception inner in aggregate.InnerExceptions) {
                BackpressureExceededException? found = FindBackpressureExceptionRecursive(inner, remainingDepth - 1);
                if (found is not null) {
                    return found;
                }
            }

            return null;
        }

        return FindBackpressureExceptionRecursive(exception.InnerException, remainingDepth - 1);
    }
}
