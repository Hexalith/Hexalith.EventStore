namespace Hexalith.EventStore.CommandApi.ErrorHandling;

using System.Text.Json;

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

public class AuthorizationExceptionHandler(ILogger<AuthorizationExceptionHandler> logger) : IExceptionHandler
{
    private const string ProblemJsonContentType = "application/problem+json";

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        if (exception is not CommandAuthorizationException authException)
        {
            return false;
        }

        string correlationId = httpContext.Items["CorrelationId"]?.ToString() ?? "unknown";

        logger.LogWarning(
            "Command authorization failed. CorrelationId={CorrelationId}, TenantId={TenantId}, Domain={Domain}, CommandType={CommandType}, Reason={Reason}",
            correlationId,
            authException.TenantId,
            authException.Domain,
            authException.CommandType,
            authException.Reason);

        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status403Forbidden,
            Title = "Forbidden",
            Type = "https://tools.ietf.org/html/rfc9457#section-3",
            Detail = authException.Reason,
            Instance = httpContext.Request.Path,
            Extensions =
            {
                ["correlationId"] = correlationId,
                ["tenantId"] = authException.TenantId,
            },
        };

        httpContext.Response.StatusCode = StatusCodes.Status403Forbidden;

        // Explicitly pass content type to WriteAsJsonAsync to ensure application/problem+json
        // (the parameterless overload always overrides ContentType to application/json)
        await httpContext.Response.WriteAsJsonAsync(problemDetails, (JsonSerializerOptions?)null, ProblemJsonContentType, cancellationToken).ConfigureAwait(false);

        return true;
    }
}
