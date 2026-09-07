using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Hexalith.EventStore.Sample.BlazorUI.Services;

/// <summary>
/// Acquires bearer tokens for the protected EventStore/query endpoints.
/// Uses Keycloak direct access grants when an authority is configured, otherwise
/// generates the development HS256 token expected by local EventStore settings.
/// </summary>
public sealed class EventStoreApiAccessTokenProvider(
    IConfiguration configuration,
    IHostEnvironment environment,
    IHttpClientFactory httpClientFactory) {
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
        string clientId = RequireConfiguration("ClientId");
        string username = RequireConfiguration("Username");
        string password = RequireConfiguration("Password");

        Uri tokenEndpoint = BuildTokenEndpoint(authority, environment.IsDevelopment());
        HttpClient client = httpClientFactory.CreateClient(nameof(EventStoreApiAccessTokenProvider));
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
        if (!environment.IsDevelopment()) {
            throw new InvalidOperationException(
                "Local token generation is available only in the Development environment; configure EventStore:Authentication:Authority.");
        }

        string issuer = RequireConfiguration("Issuer");
        string audience = RequireConfiguration("Audience");
        string signingKey = RequireConfiguration("SigningKey");
        string subject = RequireConfiguration("Subject");

        string[] tenants = RequireCollection("Tenants");
        string[] domains = RequireCollection("Domains");
        string[] permissions = RequireCollection("Permissions");

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

    internal static Uri BuildTokenEndpoint(string authority, bool allowHttp) {
        if (!Uri.TryCreate(authority.Trim(), UriKind.Absolute, out Uri? authorityUri)
            || string.IsNullOrWhiteSpace(authorityUri.Host)
            || (!string.Equals(authorityUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
                && !(allowHttp && string.Equals(authorityUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)))
            || !string.IsNullOrEmpty(authorityUri.UserInfo)
            || !string.IsNullOrEmpty(authorityUri.Query)
            || !string.IsNullOrEmpty(authorityUri.Fragment)) {
            throw new InvalidOperationException(
                "EventStore:Authentication:Authority must be an absolute HTTPS URI without user information, a query, or a fragment. HTTP is permitted only in Development.");
        }

        return new Uri(authorityUri.AbsoluteUri.TrimEnd('/') + "/protocol/openid-connect/token", UriKind.Absolute);
    }

    private string RequireConfiguration(string name) {
        string? value = configuration[$"EventStore:Authentication:{name}"];
        return !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : throw new InvalidOperationException($"EventStore:Authentication:{name} must be configured explicitly.");
    }

    private string[] RequireCollection(string name) {
        string[] values = configuration.GetSection($"EventStore:Authentication:{name}").Get<string[]>() ?? [];
        if (values.Length == 0 || values.Any(string.IsNullOrWhiteSpace)) {
            throw new InvalidOperationException(
                $"EventStore:Authentication:{name} must contain at least one non-blank value.");
        }

        return values.Select(static value => value.Trim()).ToArray();
    }

    private sealed record AccessTokenCacheEntry(string Token, DateTimeOffset ExpiresAtUtc);
}
