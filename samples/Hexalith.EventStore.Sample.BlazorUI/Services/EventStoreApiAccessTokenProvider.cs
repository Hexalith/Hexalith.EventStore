using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Hexalith.EventStore.Sample.BlazorUI.Services;

/// <summary>
/// Acquires bearer tokens for the protected CommandApi/query endpoints.
/// Uses Keycloak direct access grants when an authority is configured, otherwise
/// generates the development HS256 token expected by local CommandApi settings.
/// </summary>
public sealed class EventStoreApiAccessTokenProvider(IConfiguration configuration) {
    private readonly SemaphoreSlim _tokenLock = new(1, 1);
    private AccessTokenCacheEntry? _cachedToken;

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default) {
        if (_cachedToken is { } cached && cached.ExpiresAtUtc > DateTimeOffset.UtcNow.AddMinutes(1)) {
            return cached.Token;
        }

        await _tokenLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try {
            if (_cachedToken is { } refreshed && refreshed.ExpiresAtUtc > DateTimeOffset.UtcNow.AddMinutes(1)) {
                return refreshed.Token;
            }

            string? authority = configuration["EventStore:Authentication:Authority"];
            _cachedToken = !string.IsNullOrWhiteSpace(authority)
                ? await RequestKeycloakTokenAsync(authority, cancellationToken).ConfigureAwait(false)
                : CreateDevelopmentToken();

            return _cachedToken.Token;
        }
        finally {
            _ = _tokenLock.Release();
        }
    }

    private async Task<AccessTokenCacheEntry> RequestKeycloakTokenAsync(string authority, CancellationToken cancellationToken) {
        string clientId = configuration["EventStore:Authentication:ClientId"] ?? "hexalith-eventstore";
        string username = configuration["EventStore:Authentication:Username"]
            ?? throw new InvalidOperationException("EventStore:Authentication:Username is required when Authority is configured.");
        string password = configuration["EventStore:Authentication:Password"]
            ?? throw new InvalidOperationException("EventStore:Authentication:Password is required when Authority is configured.");

        string tokenEndpoint = authority.TrimEnd('/') + "/protocol/openid-connect/token";
        using var client = new HttpClient();
        using var form = new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("grant_type", "password"),
            new KeyValuePair<string, string>("client_id", clientId),
            new KeyValuePair<string, string>("username", username),
            new KeyValuePair<string, string>("password", password),
        ]);

        using HttpResponseMessage response = await client.PostAsync(tokenEndpoint, form, cancellationToken).ConfigureAwait(false);
        _ = response.EnsureSuccessStatusCode();

        using JsonDocument document = await response.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Keycloak token response was empty.");

        string token = document.RootElement.GetProperty("access_token").GetString()
            ?? throw new InvalidOperationException("Keycloak token response did not contain access_token.");
        int expiresIn = document.RootElement.TryGetProperty("expires_in", out JsonElement expiresElement)
            ? expiresElement.GetInt32()
            : 3600;

        return new AccessTokenCacheEntry(token, DateTimeOffset.UtcNow.AddSeconds(expiresIn));
    }

    private AccessTokenCacheEntry CreateDevelopmentToken() {
        string issuer = configuration["EventStore:Authentication:Issuer"] ?? "hexalith-dev";
        string audience = configuration["EventStore:Authentication:Audience"] ?? "hexalith-eventstore";
        string signingKey = configuration["EventStore:Authentication:SigningKey"]
            ?? throw new InvalidOperationException("EventStore:Authentication:SigningKey is required for development token generation.");
        string subject = configuration["EventStore:Authentication:Subject"] ?? "sample-blazor-ui";

        string[] tenants = configuration.GetSection("EventStore:Authentication:Tenants").Get<string[]>()
            ?? [configuration["EventStore:Counter:TenantId"] ?? "tenant-a"];
        string[] domains = configuration.GetSection("EventStore:Authentication:Domains").Get<string[]>() ?? ["counter"];
        string[] permissions = configuration.GetSection("EventStore:Authentication:Permissions").Get<string[]>()
            ?? ["command:submit", "query:read"];

        DateTimeOffset now = DateTimeOffset.UtcNow;
        DateTimeOffset expiresAt = now.AddHours(1);

        var header = new Dictionary<string, object> {
            ["alg"] = "HS256",
            ["typ"] = "JWT",
        };

        var payload = new Dictionary<string, object> {
            ["sub"] = subject,
            ["iss"] = issuer,
            ["aud"] = audience,
            ["iat"] = now.ToUnixTimeSeconds(),
            ["nbf"] = now.ToUnixTimeSeconds(),
            ["exp"] = expiresAt.ToUnixTimeSeconds(),
            ["tenants"] = JsonSerializer.Serialize(tenants),
            ["domains"] = JsonSerializer.Serialize(domains),
            ["permissions"] = JsonSerializer.Serialize(permissions),
        };

        string encodedHeader = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(header));
        string encodedPayload = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(payload));
        string unsignedToken = $"{encodedHeader}.{encodedPayload}";

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(signingKey));
        string signature = Base64UrlEncode(hmac.ComputeHash(Encoding.UTF8.GetBytes(unsignedToken)));

        return new AccessTokenCacheEntry($"{unsignedToken}.{signature}", expiresAt);
    }

    private static string Base64UrlEncode(byte[] bytes) => Convert.ToBase64String(bytes)
        .TrimEnd('=')
        .Replace('+', '-')
        .Replace('/', '_');

    private sealed record AccessTokenCacheEntry(string Token, DateTimeOffset ExpiresAtUtc);
}