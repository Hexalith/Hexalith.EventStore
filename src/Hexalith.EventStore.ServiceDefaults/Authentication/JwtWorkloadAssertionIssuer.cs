using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Hexalith.EventStore.ServiceDefaults.Authentication;

/// <summary>
/// Issues workload assertions from the trusted JWT issuer named by the shared JWT contract.
/// </summary>
/// <remarks>
/// <para>
/// In symmetric mode, which the shared contract admits only in Development or an explicit non-Production
/// break-glass environment, the shared signing key is the issuer: the assertion is signed per call with the exact
/// audience, the single operation, and any resource bindings. Every holder of that key can mint any assertion, so the
/// mode is never a production posture.
/// </para>
/// <para>
/// In authority mode the assertion is a client-credentials access token from the configured authority, requested
/// separately for every (audience, operation) pair through the scopes built by
/// <see cref="WorkloadAssertionIssuerOptions.BuildScope"/>. Before a token is attached or cached, its <c>aud</c> claim
/// must name exactly the requested audience and its <c>eventstore:operation</c> claim exactly the requested operation;
/// a broader or different token is discarded. Bound requests require an explicitly enabled authority profile;
/// its signed response must match the whole request and the enrolled workload. Bound tokens are never cached.
/// Ordinary client-credentials tokens are cached per pair until shortly before expiry.
/// </para>
/// </remarks>
public sealed partial class JwtWorkloadAssertionIssuer(
    IOptionsMonitor<JwtBearerAuthenticationOptions> contractOptions,
    IOptionsMonitor<WorkloadAssertionIssuerOptions> issuerOptions,
    IHostEnvironment environment,
    IHttpClientFactory httpClientFactory,
    WorkloadSecurityClock clock,
    ILogger<JwtWorkloadAssertionIssuer> logger) : IWorkloadAssertionIssuer, IDisposable
{
    /// <summary>Gets the named HTTP client used to reach the authority's token endpoint.</summary>
    public const string HttpClientName = "eventstore-workload-assertion-issuer";

    /// <summary>Gets the longest self-signed assertion lifetime.</summary>
    public const int MaximumSignedLifetimeSeconds = WorkloadAuthenticationOptions.DefaultMaximumLifetimeSeconds;

    private const long MaximumTokenResponseBytes = 64 * 1024;
    private static readonly TimeSpan MinimumRefreshMargin = TimeSpan.FromSeconds(30);
    private static readonly char[] OperationSeparators = [' ', '\t', '\r', '\n'];

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly ConcurrentDictionary<(string Configuration, string Audience, string Operation), CachedAssertion> _cache = new();
    private readonly SemaphoreSlim _metadataGate = new(1, 1);
    private (string Key, OpenIdConnectConfiguration Metadata, DateTimeOffset RefreshAfter)? _authorityMetadata;

    /// <inheritdoc />
    /// <remarks>Binding requires admitted symmetric Development signing or explicit enrolled authority-profile support.</remarks>
    public bool CanBindResources
    {
        get
        {
            JwtBearerAuthenticationOptions contract = contractOptions.Get(EventStoreWorkloadAuthenticationDefaults.JwtContractOptionsName);
            WorkloadAssertionIssuerOptions issuer = issuerOptions.CurrentValue;
            return !string.IsNullOrWhiteSpace(issuer.Workload)
                && (!string.IsNullOrWhiteSpace(contract.SigningKey)
                    || issuer.EnableResourceBindingRequests && issuer.HasClientCredentials())
                && JwtBearerAuthenticationContract.Validate(contract, environment, EventStoreWorkloadAuthenticationDefaults.JwtContractSection).Succeeded;
        }
    }

    /// <summary>
    /// Gets the support-safe reason a client-credentials token cannot be attached for one audience and operation, or
    /// <see langword="null"/> when the token grants exactly that pair.
    /// </summary>
    /// <param name="token">The access token returned by the authority.</param>
    /// <param name="audience">The requested audience.</param>
    /// <param name="operation">The requested operation.</param>
    /// <returns>A bounded reason, or <see langword="null"/> when the token is exactly scoped.</returns>
    public static string? GetTokenScopeFailure(string token, string audience, string operation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(audience);
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);
        JsonWebToken parsed;
        try
        {
            parsed = new JsonWebToken(token);
        }
        catch (Exception exception) when (exception is ArgumentException or SecurityTokenMalformedException)
        {
            return "token-malformed";
        }

        string[] audiences = [.. parsed.Audiences.Distinct(StringComparer.Ordinal)];
        if (audiences.Length != 1 || !string.Equals(audiences[0], audience.Trim(), StringComparison.Ordinal))
        {
            return "token-audience-mismatch";
        }

        string[] operations = [.. parsed.Claims
            .Where(static claim => string.Equals(claim.Type, EventStoreWorkloadAuthenticationDefaults.OperationClaimType, StringComparison.Ordinal))
            .SelectMany(static claim => claim.Value.Split(OperationSeparators, StringSplitOptions.RemoveEmptyEntries))
            .Distinct(StringComparer.Ordinal)];
        return operations.Length == 1 && string.Equals(operations[0], operation.Trim(), StringComparison.Ordinal)
            ? null
            : "token-operation-mismatch";
    }

    /// <inheritdoc />
    public async ValueTask<string?> IssueAsync(WorkloadAssertionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Audience);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Operation);
        if (request.Bindings is not null
            && request.Bindings.Keys.Any(static key => !key.StartsWith(EventStoreWorkloadAuthenticationDefaults.BindingClaimPrefix, StringComparison.Ordinal)))
        {
            throw new ArgumentException("Bindings must use the binding claim prefix.", nameof(request));
        }

        JwtBearerAuthenticationOptions contract = contractOptions.Get(EventStoreWorkloadAuthenticationDefaults.JwtContractOptionsName);
        if (!JwtBearerAuthenticationContract.Validate(contract, environment, EventStoreWorkloadAuthenticationDefaults.JwtContractSection).Succeeded)
        {
            LogIssuerUnavailable(logger, "contract-unusable");
            return null;
        }

        WorkloadAssertionIssuerOptions issuer = issuerOptions.CurrentValue;
        if (!string.IsNullOrWhiteSpace(contract.SigningKey))
        {
            return Sign(contract, issuer, request);
        }

        if (request.Bindings is { Count: > 0 })
        {
            if (!issuer.EnableResourceBindingRequests || !issuer.HasClientCredentials() || string.IsNullOrWhiteSpace(issuer.Workload))
            {
                LogIssuerUnavailable(logger, "bindings-unsupported");
                return null;
            }
            return await AcquireBoundAssertionAsync(contract, issuer, request, cancellationToken).ConfigureAwait(false);
        }

        if (issuer.EnableResourceBindingRequests)
        {
            if (!issuer.HasClientCredentials() || string.IsNullOrWhiteSpace(issuer.Workload)) return null;
            return await AcquireBoundAssertionAsync(contract, issuer, request, cancellationToken).ConfigureAwait(false);
        }

        return await AcquireClientCredentialsAsync(
            contract,
            issuer,
            request.Audience.Trim(),
            request.Operation.Trim(),
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _gate.Dispose();
        _metadataGate.Dispose();
    }

    private string? Sign(JwtBearerAuthenticationOptions contract, WorkloadAssertionIssuerOptions issuer, WorkloadAssertionRequest request)
    {
        if (string.IsNullOrWhiteSpace(issuer.Workload))
        {
            LogIssuerUnavailable(logger, "workload-unconfigured");
            return null;
        }

        int lifetime = Math.Clamp(issuer.LifetimeSeconds, 1, MaximumSignedLifetimeSeconds);
        DateTime now = clock.GetUtcNow().UtcDateTime;
        var claims = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            [EventStoreWorkloadAuthenticationDefaults.CallerClaimType] = issuer.Workload.Trim(),
            [EventStoreWorkloadAuthenticationDefaults.OperationClaimType] = request.Operation,
            [RouteCatalogSurfacePolicy.ClaimType] = "service",
            [JwtRegisteredClaimNames.Jti] = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant(),
        };
        if (request.Bindings is not null)
        {
            foreach (KeyValuePair<string, string> binding in request.Bindings)
            {
                claims[binding.Key] = binding.Value;
            }
        }

        byte[] key = Encoding.UTF8.GetBytes(contract.SigningKey!);
        try
        {
            var descriptor = new SecurityTokenDescriptor
            {
                Issuer = contract.Issuer.Trim(),
                Audience = request.Audience.Trim(),
                IssuedAt = now,
                NotBefore = now,
                Expires = now.AddSeconds(lifetime),
                Claims = claims,
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256),
            };
            return new JsonWebTokenHandler().CreateToken(descriptor);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    private async Task<string?> AcquireClientCredentialsAsync(
        JwtBearerAuthenticationOptions contract,
        WorkloadAssertionIssuerOptions issuer,
        string audience,
        string operation,
        CancellationToken cancellationToken)
    {
        if (!issuer.HasClientCredentials())
        {
            LogIssuerUnavailable(logger, "client-credentials-unconfigured");
            return null;
        }

        (string Configuration, string Audience, string Operation) key = (GetIssuerConfigurationKey(contract, issuer), audience, operation);
        if (TryGetCached(key, out string? cached))
        {
            return cached;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (TryGetCached(key, out cached))
            {
                return cached;
            }

            string? endpoint = await ResolveTokenEndpointAsync(contract, issuer, cancellationToken).ConfigureAwait(false);
            if (endpoint is null)
            {
                return null;
            }

            using var form = new FormUrlEncodedContent(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = issuer.ClientId!,
                ["client_secret"] = issuer.ClientSecret!,
                ["scope"] = issuer.BuildScope(audience, operation),
            });
            HttpClient client = httpClientFactory.CreateClient(HttpClientName);
            using var tokenRequest = new HttpRequestMessage(HttpMethod.Post, new Uri(endpoint)) { Content = form };
            using HttpResponseMessage response = await client.SendAsync(tokenRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                LogTokenRequestFailed(logger, (int)response.StatusCode);
                return null;
            }

            using (JsonDocument document = await ReadBoundedTokenResponseAsync(response.Content, cancellationToken).ConfigureAwait(false))
            {
                if (!document.RootElement.TryGetProperty("access_token", out JsonElement tokenElement)
                    || tokenElement.ValueKind != JsonValueKind.String
                    || string.IsNullOrWhiteSpace(tokenElement.GetString()))
                {
                    LogIssuerUnavailable(logger, "token-response-invalid");
                    return null;
                }

                string issuedAssertion = tokenElement.GetString()!;

                // Never attach a token broader than, or different from, the requested audience and operation.
                string? scopeFailure = GetTokenScopeFailure(issuedAssertion, audience, operation);
                if (scopeFailure is not null)
                {
                    LogIssuerUnavailable(logger, scopeFailure);
                    return null;
                }

                DateTimeOffset now = clock.GetUtcNow();
                TimeSpan lifetime = GetLifetime(document.RootElement, issuedAssertion, now);
                if (lifetime <= TimeSpan.Zero)
                {
                    LogIssuerUnavailable(logger, "token-expired");
                    return null;
                }

                TimeSpan margin = lifetime > MinimumRefreshMargin * 2 ? MinimumRefreshMargin : lifetime / 2;
                _cache[key] = new CachedAssertion(issuedAssertion, now.Add(lifetime - margin));
                return issuedAssertion;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or IOException or InvalidDataException or InvalidOperationException or TaskCanceledException)
        {
            LogIssuerUnavailable(logger, "token-endpoint-unavailable");
            return null;
        }
        finally
        {
            _ = _gate.Release();
        }
    }

    /// <summary>
    /// Reads the positive <c>expires_in</c> of a token response, sent as a JSON number or, as some identity providers
    /// do, as a numeric string.
    /// </summary>
    /// <param name="response">The token response root.</param>
    /// <returns>The lifetime in seconds, or <see langword="null"/> when absent, malformed, or not positive.</returns>
    internal static int? ReadExpiresIn(JsonElement response)
    {
        if (!response.TryGetProperty("expires_in", out JsonElement expires))
        {
            return null;
        }

        int seconds = 0;
        bool parsed = expires.ValueKind switch
        {
            JsonValueKind.Number => expires.TryGetInt32(out seconds),
            JsonValueKind.String => int.TryParse(expires.GetString(), NumberStyles.None, CultureInfo.InvariantCulture, out seconds),
            _ => false,
        };
        return parsed && seconds > 0 ? seconds : null;
    }

    private static TimeSpan GetLifetime(JsonElement response, string token, DateTimeOffset now)
    {
        TimeSpan lifetime = TimeSpan.FromSeconds(ReadExpiresIn(response) ?? 60);

        // The token's own expiry wins when it is earlier than the advertised lifetime.
        var parsed = new JsonWebToken(token);
        if (parsed.TryGetPayloadValue("exp", out long expiresAt) && expiresAt > 0)
        {
            TimeSpan untilExpiry = DateTimeOffset.FromUnixTimeSeconds(expiresAt) - now;
            if (untilExpiry < lifetime)
            {
                lifetime = untilExpiry;
            }
        }

        return lifetime;
    }

    private bool TryGetCached((string Configuration, string Audience, string Operation) key, out string? token)
    {
        if (_cache.TryGetValue(key, out CachedAssertion? cached) && clock.GetUtcNow() < cached.RefreshAfter)
        {
            token = cached.Token;
            return true;
        }

        token = null;
        return false;
    }

    private async Task<string?> ResolveTokenEndpointAsync(
        JwtBearerAuthenticationOptions contract,
        WorkloadAssertionIssuerOptions issuer,
        CancellationToken cancellationToken)
    {
        string? endpoint = issuer.TokenEndpoint;
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            OpenIdConnectConfiguration? metadata = await GetAuthorityMetadataAsync(contract, cancellationToken, issuer.EnableResourceBindingRequests).ConfigureAwait(false);
            endpoint = metadata?.TokenEndpoint;
        }
        if (!IsValidTokenEndpoint(endpoint))
        {
            LogIssuerUnavailable(logger, "token-endpoint-invalid");
            return null;
        }
        return new Uri(endpoint!.Trim()).AbsoluteUri;
    }

    private bool IsValidTokenEndpoint(string? endpoint) => Uri.TryCreate(endpoint?.Trim(), UriKind.Absolute, out Uri? uri)
        && string.IsNullOrEmpty(uri.UserInfo) && string.IsNullOrEmpty(uri.Fragment)
        && (uri.Scheme == Uri.UriSchemeHttps || environment.IsDevelopment() && uri.Scheme == Uri.UriSchemeHttp);

    private async Task<OpenIdConnectConfiguration?> GetAuthorityMetadataAsync(JwtBearerAuthenticationOptions contract,
        CancellationToken cancellationToken, bool requireSigningKeys = false)
    {
        string key = JsonSerializer.Serialize(new { contract.Authority, contract.Issuer, contract.RequireHttpsMetadata, contract.AllowedAlgorithms, requireSigningKeys });
        await _metadataGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            DateTimeOffset now = clock.GetUtcNow();
            if (_authorityMetadata is { } cached && cached.Key == key && now < cached.RefreshAfter) return cached.Metadata;
            // Keep one trust snapshot, not an unbounded authority registry. Expired/changed trust never falls back.
            _authorityMetadata = null;
            HttpClient client = httpClientFactory.CreateClient(HttpClientName);
            OpenIdConnectConfiguration metadata = await OpenIdConnectConfigurationRetriever.GetAsync(
                contract.Authority!.Trim().TrimEnd('/') + "/.well-known/openid-configuration",
                new HttpDocumentRetriever(client) { RequireHttps = contract.RequireHttpsMetadata }, cancellationToken).ConfigureAwait(false);
            if (metadata.Issuer != contract.Issuer.Trim() || !IsValidTokenEndpoint(metadata.TokenEndpoint)
                || requireSigningKeys && metadata.SigningKeys.Count == 0) return null;
            _authorityMetadata = (key, metadata, now.AddMinutes(5));
            return metadata;
        }
        finally { _metadataGate.Release(); }
    }

    private static string GetIssuerConfigurationKey(JwtBearerAuthenticationOptions contract, WorkloadAssertionIssuerOptions issuer)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        {
            contract.Authority, contract.Issuer, contract.RequireHttpsMetadata, contract.AllowedAlgorithms,
            issuer.ClientId, issuer.ClientSecret, issuer.TokenEndpoint, issuer.AudienceScopePrefix, issuer.OperationScopePrefix,
        }))));

    private static async Task<JsonDocument> ReadBoundedTokenResponseAsync(HttpContent content, CancellationToken cancellationToken)
    {
        if (content.Headers.ContentLength > MaximumTokenResponseBytes) throw new InvalidDataException("Token response exceeds its bound.");
        using Stream input = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var output = new MemoryStream();
        byte[] buffer = new byte[8192];
        while (true)
        {
            int requested = (int)Math.Min(buffer.Length, MaximumTokenResponseBytes + 1 - output.Length);
            int read = await input.ReadAsync(buffer.AsMemory(0, requested), cancellationToken).ConfigureAwait(false);
            if (read == 0) break;
            output.Write(buffer, 0, read);
            if (output.Length > MaximumTokenResponseBytes) throw new InvalidDataException("Token response exceeds its bound.");
        }
        return JsonDocument.Parse(output.ToArray());
    }

    [LoggerMessage(
        EventId = 5511,
        Level = LogLevel.Warning,
        Message = "Workload assertion could not be issued: Reason={Reason}. Outbound internal calls will be denied by their receivers.")]
    private static partial void LogIssuerUnavailable(ILogger logger, string reason);

    [LoggerMessage(
        EventId = 5512,
        Level = LogLevel.Warning,
        Message = "Workload assertion token request failed: StatusCode={StatusCode}.")]
    private static partial void LogTokenRequestFailed(ILogger logger, int statusCode);

    private sealed record CachedAssertion(string Token, DateTimeOffset RefreshAfter);
}
