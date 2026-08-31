using System.Security.Claims;
using System.Text.Encodings.Web;

using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.ProviderVerification;

internal sealed class ProviderVerificationAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    ProviderStateCoordinator coordinator)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    internal const string ExpectedAuthorization = "Bearer FC_CONTRACT_TOKEN";
    public const string SchemeName = "ProviderVerification";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        string path = Request.Path.Value ?? string.Empty;
        if (path is "/health" or "/alive" or "/ready" or "/__provider-state")
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        string state = SupportedProviderStates.RequireActive(coordinator);
        if (string.Equals(state, "command-unauthorized", StringComparison.Ordinal)
            || !string.Equals(Request.Headers.Authorization, ExpectedAuthorization, StringComparison.Ordinal))
        {
            return Task.FromResult(AuthenticateResult.Fail("provider-state-authentication-denied"));
        }

        string tenant = state is "command-auth-tenant" or "query-auth-tenant"
            ? "tenant-contract-case"
            : "tenant-contract-a";
        var claims = new[]
        {
            new Claim("sub", "user-contract-a"),
            new Claim("eventstore:tenant", tenant),
            new Claim("eventstore:domain", "orders"),
            new Claim("eventstore:permission", "command:*"),
            new Claim("eventstore:permission", "query:*"),
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
    }
}
