using System.Text.Json;

using Hexalith.EventStore.CommandApi.Middleware;
using Hexalith.EventStore.Server.Pipeline.Commands;

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Hexalith.EventStore.CommandApi.ErrorHandling;

public class DomainCommandRejectedExceptionHandler(ILogger<DomainCommandRejectedExceptionHandler> logger) : IExceptionHandler {
    private const string ProblemJsonContentType = "application/problem+json";

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(httpContext);

        if (exception is not DomainCommandRejectedException rejection) {
            return false;
        }

        string correlationId = httpContext.Items[CorrelationIdMiddleware.HttpContextKey]?.ToString() ?? rejection.CorrelationId;

        logger.LogWarning(
            "Domain rejection returned to caller: CorrelationId={CorrelationId}, TenantId={TenantId}, RejectionType={RejectionType}",
            correlationId,
            rejection.TenantId,
            rejection.RejectionType);

        int statusCode = GetStatusCode(rejection.RejectionType);
        string title = statusCode switch {
            StatusCodes.Status404NotFound => "Not Found",
            StatusCodes.Status409Conflict => "Conflict",
            _ => "Unprocessable Entity",
        };

        var problemDetails = new ProblemDetails {
            Status = statusCode,
            Title = title,
            Type = rejection.RejectionType,
            Detail = AuthorizationExceptionHandler.SanitizeForbiddenTerms(rejection.Detail),
            Instance = httpContext.Request.Path,
            Extensions = {
                ["correlationId"] = correlationId,
                ["tenantId"] = rejection.TenantId,
            },
        };

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, (JsonSerializerOptions?)null, ProblemJsonContentType, cancellationToken).ConfigureAwait(false);
        return true;
    }

    private static int GetStatusCode(string rejectionType) {
        if (rejectionType.EndsWith("NotFoundRejection", StringComparison.Ordinal)) {
            return StatusCodes.Status404NotFound;
        }

        if (rejectionType.EndsWith("AlreadyExistsRejection", StringComparison.Ordinal)
            || rejectionType.EndsWith("AlreadyBootstrappedRejection", StringComparison.Ordinal)) {
            return StatusCodes.Status409Conflict;
        }

        return StatusCodes.Status422UnprocessableEntity;
    }
}
