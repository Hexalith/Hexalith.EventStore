using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;

using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Admin.Server.Tests.IntegrationTests;

/// <summary>
/// Test authentication handler that reads claims from a custom X-Test-Claims header.
/// Each claim is a JSON-serialized array of {Type, Value} objects.
/// </summary>
public class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";
    public const string ClaimsHeader = "X-Test-Claims";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(ClaimsHeader, out Microsoft.Extensions.Primitives.StringValues headerValue)
            || string.IsNullOrEmpty(headerValue))
        {
            return Task.FromResult(AuthenticateResult.Fail("No test claims header found."));
        }

        try
        {
            ClaimDto[]? claimDtos = JsonSerializer.Deserialize<ClaimDto[]>(headerValue.ToString());
            if (claimDtos is null || claimDtos.Length == 0)
            {
                return Task.FromResult(AuthenticateResult.Fail("Empty claims in header."));
            }

            Claim[] claims = claimDtos.Select(c => new Claim(c.Type, c.Value)).ToArray();
            var identity = new ClaimsIdentity(claims, SchemeName);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, SchemeName);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
        catch (JsonException)
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid claims JSON."));
        }
    }

    private sealed record ClaimDto(string Type, string Value);
}
