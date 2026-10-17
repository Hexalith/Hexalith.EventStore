using System.Net;
using System.Text;
using System.Text.Json;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Hexalith.EventStore.Server.Tests.Authentication;

/// <summary>In-process test transport; never contacts a real authority or enrolls a real identity.</summary>
internal sealed class SyntheticBindingAuthorityHandler(WorkloadBindingAuthority authority, string secret, string shape) : HttpMessageHandler
{
    /// <summary>Gets token request count.</summary>
    public int TokenCalls { get; private set; }

    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        object? body;
        if (request.RequestUri!.AbsolutePath.EndsWith("/jwks", StringComparison.Ordinal)) body = authority.GetPublicKeys();
        else if (request.RequestUri.AbsolutePath.EndsWith("/openid-configuration", StringComparison.Ordinal)) body = authority.GetMetadata();
        else
        {
            TokenCalls++;
            string form = await request.Content!.ReadAsStringAsync(cancellationToken);
            Dictionary<string, string> fields = form.Split('&').Select(pair => pair.Split('=', 2)).ToDictionary(pair =>
                Uri.UnescapeDataString(pair[0].Replace('+', ' ')), pair => Uri.UnescapeDataString(pair[1].Replace('+', ' ')), StringComparer.Ordinal);
            WorkloadAuthorizationDetails details = WorkloadAuthorizationDetails.Decode(fields["authorization_details"])!;
            if (shape == "wrong-tenant") details.Bindings[EventStoreWorkloadAuthenticationDefaults.TenantBindingClaimType] = "other-tenant";
            string? token = authority.Issue(fields["client_id"], secret, fields["scope"], details);
            if (token is not null && shape == "unsigned")
            {
                var parsed = new JsonWebToken(token);
                token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
                {
                    Issuer = parsed.Issuer, Audience = parsed.Audiences.Single(), IssuedAt = parsed.IssuedAt,
                    NotBefore = parsed.ValidFrom, Expires = parsed.ValidTo,
                    Claims = parsed.Claims.Where(claim => claim.Type is not ("iss" or "aud" or "iat" or "nbf" or "exp"))
                        .ToDictionary(claim => claim.Type, claim => (object)claim.Value, StringComparer.Ordinal),
                });
            }
            body = new { access_token = token, expires_in = 120 };
        }
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
        };
    }
}
