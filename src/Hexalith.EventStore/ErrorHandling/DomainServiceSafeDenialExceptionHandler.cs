using System.Text.Json;

using Hexalith.EventStore.Contracts.Problems;
using Hexalith.EventStore.Middleware;
using Hexalith.EventStore.Server.DomainServices;

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Hexalith.EventStore.ErrorHandling;

/// <summary>
/// Returns a trusted domain service's closed denial status as metadata-only problem details. It copies the Folders
/// host's title, code, category, retryable and visibility values under EventStore's problem types, and omits the
/// detail, the downstream body and every tenant or resource identifier.
/// </summary>
/// <param name="logger">The logger that records each denial as a security event.</param>
public sealed partial class DomainServiceSafeDenialExceptionHandler(
    ILogger<DomainServiceSafeDenialExceptionHandler> logger) : IExceptionHandler
{
    private const string ProblemJsonContentType = "application/problem+json";

    /// <inheritdoc/>
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        if (exception is not DomainServiceSafeDenialException denial)
        {
            return false;
        }

        string correlationId = httpContext.Items[CorrelationIdMiddleware.HttpContextKey]?.ToString() ?? "unknown";
        LogSafeDenial(logger, correlationId, denial.StatusCode);

        (string type, string code, string title) = denial.StatusCode switch
        {
            StatusCodes.Status401Unauthorized => (ProblemTypeUris.AuthenticationRequired, "authentication_failure", "Authentication required"),
            StatusCodes.Status403Forbidden => (ProblemTypeUris.Forbidden, "authorization_denied", "Authorization denied"),
            StatusCodes.Status404NotFound => (ProblemTypeUris.NotFound, "not_found_to_caller", "Resource not available"),
            _ => (ProblemTypeUris.ServiceUnavailable, "read_model_unavailable", "Read model unavailable"),
        };
        var problem = new ProblemDetails
        {
            Type = type,
            Title = title,
            Status = denial.StatusCode,
            Extensions =
            {
                [GatewayProblemDetailsExtensions.Category] = code,
                [GatewayProblemDetailsExtensions.Code] = code,
                [GatewayProblemDetailsExtensions.Retryable] = denial.StatusCode == StatusCodes.Status503ServiceUnavailable,
                ["details"] = new Dictionary<string, string> { ["visibility"] = "metadata_only" },
            },
        };
        httpContext.Response.StatusCode = denial.StatusCode;
        await httpContext.Response.WriteAsJsonAsync(problem, (JsonSerializerOptions?)null, ProblemJsonContentType, cancellationToken)
            .ConfigureAwait(false);
        return true;
    }

    [LoggerMessage(
        EventId = 1030,
        Level = LogLevel.Warning,
        Message = "Domain service denied the request: SecurityEvent={SecurityEvent}, CorrelationId={CorrelationId}, StatusCode={StatusCode}")]
    private static partial void LogSafeDenial(
        ILogger logger,
        string correlationId,
        int statusCode,
        string securityEvent = "DomainServiceSafeDenial");
}
