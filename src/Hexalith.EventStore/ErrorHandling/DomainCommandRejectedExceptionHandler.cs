using System.Text.Json;

using Hexalith.EventStore.Contracts.Problems;
using Hexalith.EventStore.Middleware;
using Hexalith.EventStore.Server.Pipeline.Commands;

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Hexalith.EventStore.ErrorHandling;

public class DomainCommandRejectedExceptionHandler(ILogger<DomainCommandRejectedExceptionHandler> logger) : IExceptionHandler {
    private const string ProblemJsonContentType = "application/problem+json";

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(httpContext);

        if (exception is not DomainCommandRejectedException rejection) {
            return false;
        }

        string correlationId = httpContext.Items[CorrelationIdMiddleware.HttpContextKey]?.ToString() ?? rejection.CorrelationId;

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
        await httpContext.Response.WriteAsJsonAsync(problemDetails, (JsonSerializerOptions?)null, ProblemJsonContentType, cancellationToken).ConfigureAwait(false);
        return true;
    }
}
