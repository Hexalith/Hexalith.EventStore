
using System.Text.Json;
using System.Text.RegularExpressions;

using Hexalith.EventStore.CommandApi.Middleware;

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Hexalith.EventStore.CommandApi.ErrorHandling;

public partial class AuthorizationExceptionHandler(ILogger<AuthorizationExceptionHandler> logger) : IExceptionHandler {
    private const string ProblemJsonContentType = "application/problem+json";

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(httpContext);

        if (exception is not CommandAuthorizationException authException) {
            return false;
        }

        string correlationId = httpContext.Items[CorrelationIdMiddleware.HttpContextKey]?.ToString() ?? "unknown";

        logger.LogWarning(
            "Security event: SecurityEvent={SecurityEvent}, CorrelationId={CorrelationId}, Tenant={TenantId}, Domain={Domain}, CommandType={CommandType}, Reason={Reason}",
            "AuthorizationDenied",
            correlationId,
            authException.TenantId,
            authException.Domain,
            authException.CommandType,
            authException.Reason);

        string detail = CreateClientDetail(authException);

        var problemDetails = new ProblemDetails {
            Status = StatusCodes.Status403Forbidden,
            Title = "Forbidden",
            Type = ProblemTypeUris.Forbidden,
            Detail = detail,
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

    private static string CreateClientDetail(CommandAuthorizationException authException) {
        string reason = string.IsNullOrWhiteSpace(authException.Reason)
            ? "Access denied."
            : authException.Reason.Trim();

        // UX-DR6: Strip event sourcing / internal component terminology from the client-facing detail.
        // Validator reason strings (especially from actor-based validators) may contain terms like
        // "by actor", "DAPR", "aggregate", etc. that must never reach the client.
        reason = SanitizeForbiddenTerms(reason);

        if (string.IsNullOrWhiteSpace(authException.TenantId)
            || ReasonNamesTenant(reason, authException.TenantId)) {
            return reason;
        }

        return $"Not authorized for tenant '{authException.TenantId}'. {reason}";
    }

    [GeneratedRegex(@"\s+by\s+actor", RegexOptions.IgnoreCase)]
    private static partial Regex ByActorPattern();

    [GeneratedRegex(@"\bactor\b", RegexOptions.IgnoreCase)]
    private static partial Regex ActorPattern();

    [GeneratedRegex(@"\baggregate\b", RegexOptions.IgnoreCase)]
    private static partial Regex AggregatePattern();

    [GeneratedRegex(@"\bevent\s+stream\b", RegexOptions.IgnoreCase)]
    private static partial Regex EventStreamPattern();

    [GeneratedRegex(@"\bevent\s+store\b", RegexOptions.IgnoreCase)]
    private static partial Regex EventStorePattern();

    [GeneratedRegex(@"\bDAPR\b", RegexOptions.IgnoreCase)]
    private static partial Regex DaprPattern();

    [GeneratedRegex(@"\bsidecar\b", RegexOptions.IgnoreCase)]
    private static partial Regex SidecarPattern();

    [GeneratedRegex(@"\bstate\s+store\b", RegexOptions.IgnoreCase)]
    private static partial Regex StateStorePattern();

    [GeneratedRegex(@"\bpub/sub\b", RegexOptions.IgnoreCase)]
    private static partial Regex PubSubPattern();

    [GeneratedRegex(@"\s{2,}")]
    private static partial Regex MultiSpacePattern();

    internal static string SanitizeForbiddenTerms(string text) {
        if (string.IsNullOrEmpty(text)) {
            return string.Empty;
        }

        // Remove " by actor" / " by Actor" suffix (common in actor validator denial reasons)
        text = ByActorPattern().Replace(text, "");

        // Replace remaining standalone forbidden terms with safe alternatives
        text = ActorPattern().Replace(text, "service");
        text = AggregatePattern().Replace(text, "entity");
        text = EventStreamPattern().Replace(text, "data");
        text = EventStorePattern().Replace(text, "service");
        text = DaprPattern().Replace(text, "infrastructure");
        text = SidecarPattern().Replace(text, "service");
        text = StateStorePattern().Replace(text, "storage");
        text = PubSubPattern().Replace(text, "messaging");

        // Collapse any double spaces left by removals and trim
        return MultiSpacePattern().Replace(text, " ").Trim();
    }

    private static bool ReasonNamesTenant(string reason, string tenantId) =>
        reason.Contains($"tenant '{tenantId}'", StringComparison.OrdinalIgnoreCase);
}
