using System.Text.Json;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Hexalith.EventStore.ServiceDefaults.Authentication;

public sealed partial class JwtWorkloadAssertionIssuer
{
    private async Task<string?> AcquireBoundAssertionAsync(JwtBearerAuthenticationOptions contract,
        WorkloadAssertionIssuerOptions issuer, WorkloadAssertionRequest request, CancellationToken cancellationToken)
    {
        string encoded = WorkloadAuthorizationDetails.Encode(request);
        if (WorkloadAuthorizationDetails.Decode(encoded) is null) return null;
        try
        {
            string? endpoint = await ResolveTokenEndpointAsync(contract, issuer, cancellationToken).ConfigureAwait(false);
            if (endpoint is null) return null;
            HttpClient client = httpClientFactory.CreateClient(HttpClientName);
            using var form = new FormUrlEncodedContent(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["grant_type"] = "client_credentials", ["client_id"] = issuer.ClientId!, ["client_secret"] = issuer.ClientSecret!,
                ["scope"] = issuer.BuildScope(request.Audience, request.Operation), ["authorization_details"] = encoded,
            });
            using HttpResponseMessage response = await client.PostAsync(new Uri(endpoint), form, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) { LogTokenRequestFailed(logger, (int)response.StatusCode); return null; }
            await response.Content.LoadIntoBufferAsync(MaximumTokenResponseBytes, cancellationToken).ConfigureAwait(false);
            using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
            if (!document.RootElement.TryGetProperty("access_token", out JsonElement accessToken) || accessToken.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(accessToken.GetString())) return null;
            string token = accessToken.GetString()!;
            if (token.Length > EventStoreWorkloadAuthenticationDefaults.MaximumAssertionLength
                || GetTokenScopeFailure(token, request.Audience, request.Operation) is not null) return null;

            // Bound assertions are validated before use. The receiver independently validates them again.
            OpenIdConnectConfiguration metadata = await OpenIdConnectConfigurationRetriever.GetAsync(
                contract.Authority!.Trim().TrimEnd('/') + "/.well-known/openid-configuration",
                new HttpDocumentRetriever(client) { RequireHttps = contract.RequireHttpsMetadata }, cancellationToken).ConfigureAwait(false);
            if (metadata.Issuer != contract.Issuer.Trim()) return null;
            var parameters = new TokenValidationParameters
            {
                ValidateIssuer = true, ValidIssuer = contract.Issuer.Trim(), ValidateAudience = true, ValidAudience = request.Audience.Trim(),
                ValidateIssuerSigningKey = true, IssuerSigningKeys = metadata.SigningKeys,
                ValidAlgorithms = JwtBearerAuthenticationContract.ResolveAuthorityAlgorithms(contract),
                ValidateLifetime = true, RequireExpirationTime = true, RequireSignedTokens = true, ClockSkew = TimeSpan.Zero,
            };
            var handler = new JsonWebTokenHandler { MapInboundClaims = false };
            TokenValidationResult validated = await handler.ValidateTokenAsync(token, parameters).ConfigureAwait(false);
            if (!validated.IsValid) return null;
            var parsed = new JsonWebToken(token);
            DateTimeOffset now = clock.GetUtcNow();
            if (parsed.IssuedAt > now.UtcDateTime || parsed.ValidFrom > now.UtcDateTime || parsed.ValidTo <= now.UtcDateTime
                || parsed.ValidTo - parsed.IssuedAt > TimeSpan.FromSeconds(WorkloadAuthorityOptions.MaximumLifetimeSeconds)) return null;
            string[] callers = [.. parsed.Claims.Where(claim => claim.Type == EventStoreWorkloadAuthenticationDefaults.CallerClaimType).Select(claim => claim.Value)];
            if (callers.Length != 1 || callers[0] != issuer.Workload) return null;
            var bindings = parsed.Claims.Where(claim => claim.Type.StartsWith(EventStoreWorkloadAuthenticationDefaults.BindingClaimPrefix, StringComparison.Ordinal)).ToArray();
            IReadOnlyDictionary<string, string> requestedBindings = request.Bindings ?? new Dictionary<string, string>();
            if (bindings.Length != requestedBindings.Count || bindings.Select(claim => claim.Type).Distinct(StringComparer.Ordinal).Count() != bindings.Length
                || bindings.Any(claim => !requestedBindings.TryGetValue(claim.Type, out string? value) || claim.Value != value)) return null;
            return token;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or IOException or InvalidOperationException
            or TaskCanceledException or ArgumentException or SecurityTokenException)
        {
            LogIssuerUnavailable(logger, "bound-authority-unavailable");
            return null;
        }
    }
}
