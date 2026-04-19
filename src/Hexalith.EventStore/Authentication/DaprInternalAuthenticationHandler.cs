using System.Security.Claims;
using System.Text.Encodings.Web;

using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Authentication;

/// <summary>
/// Authenticates DAPR service-invocation requests from trusted internal apps. Reads the
/// <c>dapr-caller-app-id</c> header set by the calling sidecar, validates it against
/// <see cref="DaprInternalAuthenticationOptions.AllowedCallers"/>, and issues a system
/// principal with <c>global_admin</c> so the request can submit commands and queries
/// without user claims. Non-allow-listed callers return NoResult so the JWT scheme runs.
/// </summary>
public sealed class DaprInternalAuthenticationHandler(
    IOptionsMonitor<DaprInternalAuthenticationOptions> options,
    ILoggerFactory loggerFactory,
    UrlEncoder encoder) : AuthenticationHandler<DaprInternalAuthenticationOptions>(options, loggerFactory, encoder) {
    protected override Task<AuthenticateResult> HandleAuthenticateAsync() {
        string? callerAppId = Request.Headers[DaprInternalAuthenticationOptions.CallerHeaderName].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(callerAppId)) {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        if (Options.AllowedCallers.Count == 0
            || !Options.AllowedCallers.Any(c => string.Equals(c, callerAppId, StringComparison.Ordinal))) {
            Logger.LogWarning(
                "Rejecting DAPR internal auth: caller app-id '{CallerAppId}' is not in the allow-list",
                callerAppId);
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        string subject = $"system:{callerAppId}";
        Claim[] claims = [
            new(ClaimTypes.NameIdentifier, subject),
            new("sub", subject),
            new("global_admin", "true"),
            new("dapr_caller_app_id", callerAppId),
        ];

        var identity = new ClaimsIdentity(claims, Scheme.Name, ClaimTypes.NameIdentifier, ClaimTypes.Role);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
