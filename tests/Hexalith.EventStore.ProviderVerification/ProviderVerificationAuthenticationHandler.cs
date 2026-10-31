using System.Security.Claims;
using System.Text.Encodings.Web;

using Hexalith.EventStore.ServiceDefaults.Authentication;

using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.ProviderVerification;

internal sealed class ProviderVerificationAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    ProviderStateCoordinator coordinator,
    ProviderVerificationCredential credential)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
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
            || !string.Equals(
                Request.Headers.Authorization,
                string.Concat("Bearer", " ", credential.AccessToken),
                StringComparison.Ordinal))
        {
            return Task.FromResult(AuthenticateResult.Fail("provider-state-authentication-denied"));
        }

        string tenant = state is "command-auth-tenant" or "query-auth-tenant"
            ? "tenant-contract-case"
            : "tenant-contract-a";
        List<Claim> claims =
        [
            new Claim("sub", "user-contract-a"),
            new Claim("eventstore:tenant", tenant),
            new Claim("eventstore:domain", "orders"),
            new Claim("eventstore:permission", "command:*"),
            new Claim("eventstore:permission", "query:*"),
        ];
        if (credential.IncludeSurface)
        {
            claims.Add(new Claim(RouteCatalogSurfacePolicy.ClaimType, "ui"));
        }
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
    }
}
