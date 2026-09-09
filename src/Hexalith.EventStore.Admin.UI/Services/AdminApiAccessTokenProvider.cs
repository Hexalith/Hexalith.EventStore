using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Admin.Abstractions.Models.Common;

namespace Hexalith.EventStore.Admin.UI.Services;

/// <summary>
/// Acquires bearer tokens for the protected Admin.Server REST API endpoints.
/// Uses the configured or discovered OIDC token endpoint when an authority is configured, otherwise
/// generates a development HS256 token.
/// </summary>
public sealed class AdminApiAccessTokenProvider {
    private const string ClientCredentialsGrantType = "client_credentials";
    private const string PasswordGrantType = "password";
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _environment;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly DevelopmentAdminRoleState? _roleState;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);
    private int _tokenVersion;
    private AccessTokenCacheEntry? _cachedToken;

    public AdminApiAccessTokenProvider(
        IConfiguration configuration,
        IHostEnvironment environment,
        IHttpClientFactory httpClientFactory,
        DevelopmentAdminRoleState? roleState = null) {
        _configuration = configuration;
        _environment = environment;
        _httpClientFactory = httpClientFactory;
        _roleState = roleState;
        _roleState?.RoleChanged += _ => InvalidateCache();
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default) {
        while (true) {
            int observedVersion = Volatile.Read(ref _tokenVersion);
            if (_cachedToken is { } cached
                && cached.Version == observedVersion
                && cached.ExpiresAtUtc > DateTimeOffset.UtcNow.AddMinutes(1)) {
                return cached.Token;
            }

            await _tokenLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try {
                observedVersion = Volatile.Read(ref _tokenVersion);
                if (_cachedToken is { } refreshed
                    && refreshed.Version == observedVersion
                    && refreshed.ExpiresAtUtc > DateTimeOffset.UtcNow.AddMinutes(1)) {
                    return refreshed.Token;
                }

                string? authority = _configuration["EventStore:Authentication:Authority"];
                AccessTokenCacheEntry newToken = !string.IsNullOrWhiteSpace(authority)
                    ? await RequestAuthorityTokenAsync(authority, observedVersion, cancellationToken).ConfigureAwait(false)
                    : CreateDevelopmentToken(observedVersion);

                if (observedVersion != Volatile.Read(ref _tokenVersion)) {
                    _cachedToken = null;
                    continue;
                }

                _cachedToken = newToken;
                return newToken.Token;
            }
            finally {
                _ = _tokenLock.Release();
            }
        }
    }

    private async Task<AccessTokenCacheEntry> RequestAuthorityTokenAsync(
        string authority,
        int version,
        CancellationToken cancellationToken) {
        bool allowHttp = _environment.IsDevelopment();
        Uri authorityUri = ValidateEndpoint(authority, allowHttp, "Authority", allowQuery: false);
        string grantType = RequireTextConfiguration("GrantType");
        string clientId = RequireTextConfiguration("ClientId");
        string scope = RequireTextConfiguration("Scope");
        var formValues = new List<KeyValuePair<string, string>> {
            new("grant_type", grantType),
            new("client_id", clientId),
            new("scope", scope),
        };

        switch (grantType) {
            case PasswordGrantType:
                formValues.Add(new("username", RequireTextConfiguration("Username")));
                formValues.Add(new("password", RequireOpaqueConfiguration("Password")));
                break;
            case ClientCredentialsGrantType:
                formValues.Add(new("client_secret", RequireOpaqueConfiguration("ClientSecret")));
                break;
            default:
                throw new InvalidOperationException(
                    "EventStore:Authentication:GrantType must be either 'password' or 'client_credentials'.");
        }

        AddOptionalAudienceParameter(formValues);

        HttpClient client = _httpClientFactory.CreateClient(nameof(AdminApiAccessTokenProvider));
        Uri tokenEndpoint = await ResolveTokenEndpointAsync(
            client,
            authorityUri,
            allowHttp,
            cancellationToken).ConfigureAwait(false);
        using var form = new FormUrlEncodedContent(formValues);

        using HttpResponseMessage response = await client.PostAsync(tokenEndpoint, form, cancellationToken).ConfigureAwait(false);
        _ = response.EnsureSuccessStatusCode();

        using JsonDocument document = await response.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("OIDC token response was empty.");

        string? token = document.RootElement.TryGetProperty("access_token", out JsonElement tokenElement)
            ? tokenElement.GetString()
            : null;
        if (string.IsNullOrWhiteSpace(token)) {
            throw new InvalidOperationException("OIDC token response did not contain a non-blank access_token.");
        }
        int expiresIn = document.RootElement.TryGetProperty("expires_in", out JsonElement expiresElement)
            ? expiresElement.GetInt32()
            : 3600;

        return new AccessTokenCacheEntry(token, DateTimeOffset.UtcNow.AddSeconds(expiresIn), version);
    }

    private AccessTokenCacheEntry CreateDevelopmentToken(int version) {
        if (!_environment.IsDevelopment()) {
            throw new InvalidOperationException(
                "Local token generation is available only in the Development environment; configure EventStore:Authentication:Authority.");
        }

        string issuer = RequireTextConfiguration("Issuer");
        string audience = RequireTextConfiguration("Audience");
        string signingKey = RequireOpaqueConfiguration("SigningKey");
        string subject = RequireTextConfiguration("Subject");
        bool globalAdmin = _configuration.GetValue("EventStore:Authentication:GlobalAdmin", defaultValue: false);

        string[] tenants = RequireCollection("Tenants");
        string[] domains = RequireCollection("Domains");
        string[] permissions = RequireCollection("Permissions");
        AdminRole role = _roleState?.IsRoleSwitcherAvailable == true
            ? _roleState.SelectedRole
            : AdminRole.Admin;

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
            [AdminClaimTypes.Role] = role.ToString(),
        };

        if (globalAdmin && role == AdminRole.Admin) {
            payload["global_admin"] = true;
        }

        string encodedHeader = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(header));
        string encodedPayload = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(payload));
        string unsignedToken = $"{encodedHeader}.{encodedPayload}";

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(signingKey));
        string signature = Base64UrlEncode(hmac.ComputeHash(Encoding.UTF8.GetBytes(unsignedToken)));

        return new AccessTokenCacheEntry($"{unsignedToken}.{signature}", expiresAt, version);
    }

    private void InvalidateCache() {
        _ = Interlocked.Increment(ref _tokenVersion);
        _cachedToken = null;
    }

    private static string Base64UrlEncode(byte[] bytes) => Convert.ToBase64String(bytes)
        .TrimEnd('=')
        .Replace('+', '-')
        .Replace('/', '_');

    internal static Uri BuildTokenEndpoint(string endpoint, bool allowHttp)
        => ValidateEndpoint(endpoint, allowHttp, "OIDC endpoint", allowQuery: true);

    private void AddOptionalAudienceParameter(List<KeyValuePair<string, string>> formValues) {
        string? configuredName = _configuration["EventStore:Authentication:AudienceParameterName"];
        string? configuredValue = _configuration["EventStore:Authentication:AudienceParameterValue"];
        bool hasName = !string.IsNullOrWhiteSpace(configuredName);
        bool hasValue = !string.IsNullOrWhiteSpace(configuredValue);
        if (hasName != hasValue) {
            throw new InvalidOperationException(
                "EventStore:Authentication:AudienceParameterName and AudienceParameterValue must be configured together.");
        }

        if (!hasName) {
            return;
        }

        string parameterName = configuredName!.Trim();
        if (parameterName is not "audience" and not "resource") {
            throw new InvalidOperationException(
                "EventStore:Authentication:AudienceParameterName must be either 'audience' or 'resource'.");
        }

        formValues.Add(new(parameterName, configuredValue!.Trim()));
    }

    private async Task<Uri> ResolveTokenEndpointAsync(
        HttpClient client,
        Uri authorityUri,
        bool allowHttp,
        CancellationToken cancellationToken) {
        string? configuredEndpoint = _configuration["EventStore:Authentication:TokenEndpoint"];
        if (!string.IsNullOrWhiteSpace(configuredEndpoint)) {
            return ValidateEndpoint(configuredEndpoint, allowHttp, "TokenEndpoint", allowQuery: true);
        }

        Uri discoveryEndpoint = new(
            authorityUri.AbsoluteUri.TrimEnd('/') + "/.well-known/openid-configuration",
            UriKind.Absolute);
        using HttpResponseMessage response = await client.GetAsync(discoveryEndpoint, cancellationToken).ConfigureAwait(false);
        _ = response.EnsureSuccessStatusCode();
        using JsonDocument document = await response.Content
            .ReadFromJsonAsync<JsonDocument>(cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("OIDC discovery response was empty.");
        string? issuer = document.RootElement.TryGetProperty("issuer", out JsonElement issuerElement)
            ? issuerElement.GetString()
            : null;
        if (string.IsNullOrWhiteSpace(issuer)) {
            throw new InvalidOperationException("OIDC discovery did not contain a non-blank issuer.");
        }

        Uri discoveredIssuer = ValidateEndpoint(issuer, allowHttp, "discovery issuer", allowQuery: false);
        if (!string.Equals(
            NormalizeEndpointForComparison(authorityUri),
            NormalizeEndpointForComparison(discoveredIssuer),
            StringComparison.Ordinal)) {
            throw new InvalidOperationException(
                "OIDC discovery issuer must match EventStore:Authentication:Authority.");
        }

        string? tokenEndpoint = document.RootElement.TryGetProperty("token_endpoint", out JsonElement tokenEndpointElement)
            ? tokenEndpointElement.GetString()
            : null;
        if (string.IsNullOrWhiteSpace(tokenEndpoint)) {
            throw new InvalidOperationException("OIDC discovery did not contain a non-blank token_endpoint.");
        }

        return ValidateEndpoint(tokenEndpoint, allowHttp, "discovery token_endpoint", allowQuery: true);
    }

    private static string NormalizeEndpointForComparison(Uri endpoint)
        => endpoint.GetComponents(
                UriComponents.SchemeAndServer | UriComponents.Path,
                UriFormat.UriEscaped)
            .TrimEnd('/');

    private static Uri ValidateEndpoint(
        string endpoint,
        bool allowHttp,
        string settingName,
        bool allowQuery) {
        if (!Uri.TryCreate(endpoint.Trim(), UriKind.Absolute, out Uri? endpointUri)
            || string.IsNullOrWhiteSpace(endpointUri.Host)
            || (!string.Equals(endpointUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
                && !(allowHttp && string.Equals(endpointUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)))
            || !string.IsNullOrEmpty(endpointUri.UserInfo)
            || (!allowQuery && !string.IsNullOrEmpty(endpointUri.Query))
            || !string.IsNullOrEmpty(endpointUri.Fragment)) {
            throw new InvalidOperationException(
                allowQuery
                    ? $"EventStore:Authentication:{settingName} must be an absolute HTTPS URI without user information or a fragment. HTTP is permitted only in Development."
                    : $"EventStore:Authentication:{settingName} must be an absolute HTTPS URI without user information, a query, or a fragment. HTTP is permitted only in Development.");
        }

        return new Uri(endpointUri.AbsoluteUri.TrimEnd('/'), UriKind.Absolute);
    }

    private string RequireTextConfiguration(string name) {
        string? value = _configuration[$"EventStore:Authentication:{name}"];
        return !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : throw new InvalidOperationException($"EventStore:Authentication:{name} must be configured explicitly.");
    }

    private string RequireOpaqueConfiguration(string name) {
        string? value = _configuration[$"EventStore:Authentication:{name}"];
        return !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new InvalidOperationException($"EventStore:Authentication:{name} must be configured explicitly.");
    }

    private string[] RequireCollection(string name) {
        string[] values = _configuration.GetSection($"EventStore:Authentication:{name}").Get<string[]>() ?? [];
        if (values.Length == 0 || values.Any(string.IsNullOrWhiteSpace)) {
            throw new InvalidOperationException(
                $"EventStore:Authentication:{name} must contain at least one non-blank value.");
        }

        return values.Select(static value => value.Trim()).ToArray();
    }

    private sealed record AccessTokenCacheEntry(string Token, DateTimeOffset ExpiresAtUtc, int Version);
}
