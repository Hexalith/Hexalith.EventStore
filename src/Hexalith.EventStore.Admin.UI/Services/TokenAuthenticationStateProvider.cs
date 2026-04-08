using System.Security.Claims;
using System.Text.Json;

using Microsoft.AspNetCore.Components.Authorization;

namespace Hexalith.EventStore.Admin.UI.Services;

/// <summary>
/// Provides Blazor authentication state by decoding the JWT acquired
/// by <see cref="AdminApiAccessTokenProvider"/>. In a Blazor Server app
/// the default <c>ServerAuthenticationStateProvider</c> reads from
/// HttpContext which has no JWT (WebSocket connection). This provider
/// bridges the gap by parsing the server-side token into a ClaimsPrincipal.
/// </summary>
public sealed class TokenAuthenticationStateProvider(
    AdminApiAccessTokenProvider tokenProvider,
    ILogger<TokenAuthenticationStateProvider> logger) : AuthenticationStateProvider
{
    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        try
        {
            string token = await tokenProvider.GetAccessTokenAsync().ConfigureAwait(false);
            ClaimsPrincipal principal = ParseJwtClaims(token);
            logger.LogInformation(
                "TokenAuthenticationStateProvider: authenticated={IsAuthenticated}, claims={ClaimCount}",
                principal.Identity?.IsAuthenticated ?? false,
                principal.Claims.Count());
            return new AuthenticationState(principal);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "TokenAuthenticationStateProvider: failed to acquire token, returning anonymous user.");
            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
        }
    }

    private static ClaimsPrincipal ParseJwtClaims(string token)
    {
        string[] parts = token.Split('.');
        if (parts.Length != 3)
        {
            return new ClaimsPrincipal(new ClaimsIdentity());
        }

        byte[] payloadBytes = DecodeBase64Url(parts[1]);
        using JsonDocument document = JsonDocument.Parse(payloadBytes);
        JsonElement root = document.RootElement;

        var claims = new List<Claim>();
        foreach (JsonProperty property in root.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement element in property.Value.EnumerateArray())
                {
                    claims.Add(new Claim(property.Name, element.ToString()));
                }
            }
            else if (property.Value.ValueKind == JsonValueKind.String)
            {
                claims.Add(new Claim(property.Name, property.Value.GetString()!));
            }
            else
            {
                claims.Add(new Claim(property.Name, property.Value.GetRawText()));
            }
        }

        var identity = new ClaimsIdentity(claims, "JWT");
        return new ClaimsPrincipal(identity);
    }

    private static byte[] DecodeBase64Url(string base64Url)
    {
        string padded = base64Url
            .Replace('-', '+')
            .Replace('_', '/');
        switch (padded.Length % 4)
        {
            case 2: padded += "=="; break;
            case 3: padded += "="; break;
        }

        return Convert.FromBase64String(padded);
    }
}
