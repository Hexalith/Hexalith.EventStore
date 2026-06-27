using System.Text.Json;

using Hexalith.EventStore.Contracts.Problems;
using Hexalith.EventStore.Middleware;
using Hexalith.EventStore.Server.Pipeline.Commands;

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Hexalith.EventStore.ErrorHandling;

public class DomainCommandRejectedExceptionHandler(ILogger<DomainCommandRejectedExceptionHandler> logger) : IExceptionHandler {
    private const string _problemJsonContentType = "application/problem+json";

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(httpContext);

        if (exception is not DomainCommandRejectedException rejection) {
            return false;
        }

        string correlationId = httpContext.Items[CorrelationIdMiddleware.HttpContextKey]?.ToString() ?? rejection.CorrelationId;

        // A domain rejection is an expected outcome. If the caller already disconnected (or the response
        // has already started), writing the ProblemDetails throws — and an OperationCanceledException
        // escaping this handler is surfaced by the framework as a misleading "unhandled exception" plus a
        // failed-error-handler log. Treat those cases as already handled instead of cascading.
        if (httpContext.RequestAborted.IsCancellationRequested || httpContext.Response.HasStarted) {
            logger.LogInformation(
                "Domain rejection not written; caller already disconnected: CorrelationId={CorrelationId}, TenantId={TenantId}, RejectionType={RejectionType}",
                correlationId,
                rejection.TenantId,
                rejection.RejectionType);
            return true;
        }

        logger.LogInformation(
            "Domain rejection returned to caller: CorrelationId={CorrelationId}, TenantId={TenantId}, RejectionType={RejectionType}",
            correlationId,
            rejection.TenantId,
            rejection.RejectionType);

        DomainRejectionProblem problem = DomainRejectionProblemCatalog.FromRejectionType(rejection.RejectionType);

        var problemDetails = new ProblemDetails {
            Status = problem.StatusCode,
            Title = problem.Title,
            Type = problem.TypeUri,
            Detail = problem.Explanation,
            Instance = httpContext.Request.Path,
            Extensions = {
                [GatewayProblemDetailsExtensions.CorrelationId] = correlationId,
                [GatewayProblemDetailsExtensions.TenantId] = rejection.TenantId,
                [GatewayProblemDetailsExtensions.ReasonCode] = problem.ReasonCode,
                [GatewayProblemDetailsExtensions.RejectionType] = rejection.RejectionType,
                [GatewayProblemDetailsExtensions.CorrectiveAction] = problem.CorrectiveAction,
            },
        };

        httpContext.Response.StatusCode = problem.StatusCode;
        try {
            await httpContext.Response.WriteAsJsonAsync(problemDetails, (JsonSerializerOptions?)null, _problemJsonContentType, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) {
            // Caller disconnected mid-write; the rejection is still considered handled.
        }

        return true;
    }
}
