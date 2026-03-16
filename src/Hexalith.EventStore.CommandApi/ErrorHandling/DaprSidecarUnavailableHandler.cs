
using System.Net.Http;
using System.Net.Sockets;
using System.Text.Json;

using Dapr;

using Grpc.Core;

using Hexalith.EventStore.CommandApi.Middleware;

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Hexalith.EventStore.CommandApi.ErrorHandling;

/// <summary>
/// Handles DAPR sidecar unavailability by returning 503 Service Unavailable.
/// Detects gRPC Unavailable status codes and HTTP connection failures in the exception chain.
/// SECURITY: The response body contains a generic message only — no DAPR, sidecar, or actor details.
/// </summary>
public class DaprSidecarUnavailableHandler(
    ILogger<DaprSidecarUnavailableHandler> logger) : IExceptionHandler {
    private const string ProblemJsonContentType = "application/problem+json";

    /// <inheritdoc/>
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(exception);

        if (!IsSidecarUnavailable(exception)) {
            return false;
        }

        string correlationId = httpContext.Items[CorrelationIdMiddleware.HttpContextKey]?.ToString() ?? "unknown";

        // Log at Error level with FULL internal details (server-side only)
        logger.LogError(
            exception,
            "DAPR sidecar unavailable: SecurityEvent={SecurityEvent}, CorrelationId={CorrelationId}",
            "DaprSidecarUnavailable",
            correlationId);

        httpContext.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        httpContext.Response.Headers.RetryAfter = "30"; // UX-DR5: fixed 30s for 503

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
        await httpContext.Response.WriteAsJsonAsync(problemDetails, (JsonSerializerOptions?)null, ProblemJsonContentType, CancellationToken.None)
            .ConfigureAwait(false);

        return true;
    }

    /// <summary>
    /// Walks the InnerException chain looking for signs of DAPR sidecar unavailability.
    /// Same traversal pattern as <see cref="ConcurrencyConflictExceptionHandler"/>.
    /// Limits depth to 10 to prevent infinite loops on circular exception references.
    /// </summary>
    private static bool IsSidecarUnavailable(Exception? exception) {
        const int maxDepth = 10;
        return IsSidecarUnavailableRecursive(exception, maxDepth, false);
    }

    private static bool IsSidecarUnavailableRecursive(Exception? exception, int remainingDepth, bool hasDaprContext) {
        if (exception is null || remainingDepth <= 0) {
            return false;
        }

        bool currentHasDaprContext = hasDaprContext || IsDaprException(exception);

        if (exception is RpcException rpcEx
            && rpcEx.StatusCode == Grpc.Core.StatusCode.Unavailable
            && (currentHasDaprContext || HasSidecarUnavailableMessage(rpcEx.Status.Detail))) {
            return true;
        }

        if (exception is HttpRequestException httpRequestException
            && ContainsConnectionRefusedSocketException(httpRequestException.InnerException)
            && (currentHasDaprContext || HasSidecarEndpointHint(httpRequestException.Message))) {
            return true;
        }

        if (currentHasDaprContext
            && exception is DaprException daprException
            && daprException.InnerException is null
            && HasSidecarUnavailableMessage(daprException.Message)) {
            return true;
        }

        // AggregateException has multiple InnerExceptions — search all of them
        if (exception is AggregateException aggregate) {
            foreach (Exception inner in aggregate.InnerExceptions) {
                if (IsSidecarUnavailableRecursive(inner, remainingDepth - 1, currentHasDaprContext)) {
                    return true;
                }
            }

            return false;
        }

        return IsSidecarUnavailableRecursive(exception.InnerException, remainingDepth - 1, currentHasDaprContext);
    }

    /// <summary>
    /// Walks an inner exception chain looking for a <see cref="SocketException"/>.
    /// Handles the common .NET HTTP stack chain: HttpRequestException -> IOException -> SocketException,
    /// as well as AggregateException wrappers.
    /// </summary>
    private static bool ContainsConnectionRefusedSocketException(Exception? exception) {
        return ContainsConnectionRefusedSocketExceptionRecursive(exception, 5);
    }

    private static bool ContainsConnectionRefusedSocketExceptionRecursive(Exception? exception, int remainingDepth) {
        if (exception is null || remainingDepth <= 0) {
            return false;
        }

        if (exception is SocketException socketException
            && (socketException.SocketErrorCode == SocketError.ConnectionRefused
                || socketException.SocketErrorCode == SocketError.ConnectionReset)) {
            return true;
        }

        if (exception is AggregateException agg) {
            foreach (Exception inner in agg.InnerExceptions) {
                if (ContainsConnectionRefusedSocketExceptionRecursive(inner, remainingDepth - 1)) {
                    return true;
                }
            }
            return false;
        }

        return ContainsConnectionRefusedSocketExceptionRecursive(exception.InnerException, remainingDepth - 1);
    }

    private static bool IsDaprException(Exception exception) => exception is DaprException;

    private static bool HasSidecarUnavailableMessage(string? message) =>
        !string.IsNullOrWhiteSpace(message)
        && (message.Contains("sidecar", StringComparison.OrdinalIgnoreCase)
            || message.Contains("connect", StringComparison.OrdinalIgnoreCase)
            || message.Contains("unavailable", StringComparison.OrdinalIgnoreCase));

    private static bool HasSidecarEndpointHint(string? message) =>
        !string.IsNullOrWhiteSpace(message)
        && (message.Contains(":3500", StringComparison.OrdinalIgnoreCase)
            || message.Contains(":50001", StringComparison.OrdinalIgnoreCase)
            || message.Contains("/v1.0/", StringComparison.OrdinalIgnoreCase)
            || message.Contains("dapr", StringComparison.OrdinalIgnoreCase)
            || message.Contains("sidecar", StringComparison.OrdinalIgnoreCase));
}
