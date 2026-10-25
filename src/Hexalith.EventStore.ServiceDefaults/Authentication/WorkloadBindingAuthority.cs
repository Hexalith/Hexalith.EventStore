using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Hexalith.EventStore.ServiceDefaults.Authentication;

/// <summary>Issues per-request RSA assertions only after current configured grant and exact binding authorization.</summary>
public sealed class WorkloadBindingAuthority(IOptionsMonitor<WorkloadAuthorityOptions> options,
    IHostEnvironment environment, TimeProvider timeProvider)
{
    private string? _mappedPrefix;

    /// <summary>Authenticates one protected client and authorizes the whole requested scope using one current grant.</summary>
    /// <param name="clientId">The supplied client identifier.</param>
    /// <param name="clientSecret">The protected high-entropy secret.</param>
    /// <param name="scope">The exact OAuth audience/operation scope.</param>
    /// <param name="details">The decoded bounded request.</param>
    /// <returns>An assertion or null; failure never discloses grant, credential or key details.</returns>
    public string? Issue(string clientId, string clientSecret, string scope, WorkloadAuthorizationDetails details)
    {
        ArgumentNullException.ThrowIfNull(details);
        WorkloadAuthorityOptions current = options.CurrentValue;
        DateTimeOffset now = timeProvider.GetUtcNow();
        if (!Usable(current) || details.Type != WorkloadAuthorizationDetails.Profile
            || string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret)
            || clientSecret.Length < 32 || clientSecret.Length > 4096
            || scope != new WorkloadAssertionIssuerOptions().BuildScope(details.Audience, details.Operation)
            || WorkloadAuthorizationDetails.Decode(WorkloadAuthorizationDetails.Encode(new(details.Audience, details.Operation, details.Bindings))) is null)
            return null;
        WorkloadAuthorityGrant[] candidates = [.. current.Grants.Where(grant => grant is not null && grant.ClientId == clientId
            && !grant.Revoked && grant.Revision > 0 && !string.IsNullOrWhiteSpace(grant.GrantId)
            && !string.IsNullOrWhiteSpace(grant.Workload) && now >= grant.NotBefore && now < grant.ExpiresAt
            && grant.Audience == details.Audience && grant.Operations is not null && grant.Bindings is not null
            && grant.Operations.Contains(details.Operation, StringComparer.Ordinal)
            && PermitsBindings(grant, details))];
        if (candidates.Length != 1 || !MatchesSecret(clientSecret, candidates[0].ClientSecretSha256)) return null;
        WorkloadAuthorityGrant approved = candidates[0];
        try
        {
            using X509Certificate2 certificate = LoadCertificate(current, now);
            using RSA? rsa = certificate.GetRSAPrivateKey();
            if (rsa is null || rsa.KeySize < 2048) return null;
            var claims = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                [EventStoreWorkloadAuthenticationDefaults.CallerClaimType] = approved.Workload,
                [EventStoreWorkloadAuthenticationDefaults.OperationClaimType] = details.Operation,
                [JwtRegisteredClaimNames.Jti] = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)),
                ["eventstore:grant-id"] = approved.GrantId,
                ["eventstore:grant-revision"] = approved.Revision,
            };
            foreach (KeyValuePair<string, string> binding in details.Bindings) claims.Add(binding.Key, binding.Value);
            DateTimeOffset expiry = now.AddSeconds(Math.Clamp(current.LifetimeSeconds, 1, WorkloadAuthorityOptions.MaximumLifetimeSeconds));
            if (approved.ExpiresAt < expiry) expiry = approved.ExpiresAt;
            if (certificate.NotAfter.ToUniversalTime() < expiry.UtcDateTime) expiry = certificate.NotAfter.ToUniversalTime();
            var key = new RsaSecurityKey(rsa) { KeyId = current.KeyId };
            var credentials = new SigningCredentials(key, SecurityAlgorithms.RsaSha256)
            {
                CryptoProviderFactory = new CryptoProviderFactory { CacheSignatureProviders = false },
            };
            return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
            {
                Issuer = current.Issuer, Audience = details.Audience, IssuedAt = now.UtcDateTime,
                NotBefore = now.UtcDateTime, Expires = expiry.UtcDateTime, Claims = claims,
                SigningCredentials = credentials,
            });
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or CryptographicException or ArgumentException)
        {
            return null;
        }
    }

    /// <summary>Returns public discovery metadata for the explicitly configured issuer; never uses the request Host header.</summary>
    /// <returns>Public metadata, or null when disabled/unconfigured.</returns>
    public object? GetMetadata()
    {
        WorkloadAuthorityOptions current = options.CurrentValue;
        return Usable(current) ? new
        {
            issuer = current.Issuer, token_endpoint = current.Issuer + "/token", jwks_uri = current.Issuer + "/jwks",
            grant_types_supported = new[] { "client_credentials" }, token_endpoint_auth_methods_supported = new[] { "client_secret_post" },
            authorization_details_types_supported = new[] { WorkloadAuthorizationDetails.Profile },
            id_token_signing_alg_values_supported = new[] { "RS256" },
        } : null;
    }

    /// <summary>Returns only the enrolled certificate's public RSA key; no private key or certificate password leaves this service.</summary>
    /// <returns>A JWKS document, or null when the protected signer is unusable.</returns>
    public object? GetPublicKeys()
    {
        WorkloadAuthorityOptions current = options.CurrentValue;
        if (!Usable(current)) return null;
        try
        {
            using X509Certificate2 certificate = LoadCertificate(current, timeProvider.GetUtcNow());
            using RSA? rsa = certificate.GetRSAPublicKey();
            if (rsa is null || rsa.KeySize < 2048) return null;
            RSAParameters key = rsa.ExportParameters(false);
            return new { keys = new[] { new { kty = "RSA", use = "sig", alg = "RS256", kid = current.KeyId,
                n = Base64UrlEncoder.Encode(key.Modulus!), e = Base64UrlEncoder.Encode(key.Exponent!) } } };
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or CryptographicException or ArgumentException)
        {
            return null;
        }
    }

    internal void BindRoutePrefix(string prefix)
    {
        WorkloadAuthorityOptions current = options.CurrentValue;
        if (!Uri.TryCreate(current.Issuer, UriKind.Absolute, out Uri? issuer) || issuer.AbsolutePath != prefix)
            throw new InvalidOperationException("Authentication:WorkloadAuthority:Issuer path must match the mapped authority prefix.");
        _mappedPrefix = prefix;
    }

    private bool Usable(WorkloadAuthorityOptions current) => current.Enabled && current.Grants is not null
        && !string.IsNullOrWhiteSpace(current.KeyId) && !string.IsNullOrWhiteSpace(current.SigningCertificatePath)
        && Path.IsPathFullyQualified(current.SigningCertificatePath)
        && Uri.TryCreate(current.Issuer, UriKind.Absolute, out Uri? uri)
        && string.IsNullOrEmpty(uri.UserInfo) && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment)
        && current.Issuer == uri.AbsoluteUri.TrimEnd('/')
        && (_mappedPrefix is null || uri.AbsolutePath == _mappedPrefix)
        && (uri.Scheme == "https" || environment.IsDevelopment() && uri.Scheme == "http");

    private static X509Certificate2 LoadCertificate(WorkloadAuthorityOptions current, DateTimeOffset now)
    {
        X509Certificate2 certificate = X509CertificateLoader.LoadPkcs12FromFile(current.SigningCertificatePath,
            current.SigningCertificatePassword, X509KeyStorageFlags.EphemeralKeySet);
        if (!certificate.HasPrivateKey || now.UtcDateTime < certificate.NotBefore.ToUniversalTime()
            || now.UtcDateTime >= certificate.NotAfter.ToUniversalTime()
            || certificate.Extensions.OfType<X509KeyUsageExtension>().Any(usage => !usage.KeyUsages.HasFlag(X509KeyUsageFlags.DigitalSignature)))
        {
            certificate.Dispose();
            throw new CryptographicException("Signing certificate is unavailable.");
        }
        return certificate;
    }

    private static bool MatchesSecret(string secret, string expected)
    {
        if (expected.Length != 64 || !expected.All(Uri.IsHexDigit)) return false;
        byte[] actual = SHA256.HashData(Encoding.UTF8.GetBytes(secret));
        try { return CryptographicOperations.FixedTimeEquals(actual, Convert.FromHexString(expected)); }
        finally { CryptographicOperations.ZeroMemory(actual); }
    }

    private static bool PermitsBindings(WorkloadAuthorityGrant grant, WorkloadAuthorizationDetails details)
    {
        bool commandDigest = details.Bindings.ContainsKey(CommandSourceAuthorityBinding.DigestClaim);
        bool publicationDigest = details.Bindings.ContainsKey(SourcePublicationWorkloadBinding.DigestClaim);
        if (grant.AllowCompleteCommandSourceDigest && details.Operation == EventStoreWorkloadOperations.DomainServiceProcess && !commandDigest) return false;
        if (grant.AllowSourcePublicationDigest && details.Operation is SourcePublicationWorkloadBinding.PrimaryOperation or SourcePublicationWorkloadBinding.AuthorityOperation && !publicationDigest) return false;
        if (commandDigest || publicationDigest)
        {
            string dynamicClaim = commandDigest ? CommandSourceAuthorityBinding.DigestClaim : SourcePublicationWorkloadBinding.DigestClaim;
            string[] required = commandDigest
                ? [EventStoreWorkloadAuthenticationDefaults.TenantBindingClaimType, EventStoreWorkloadAuthenticationDefaults.DomainBindingClaimType,
                    CommandSourceAuthorityBinding.SourceClaim, CommandSourceAuthorityBinding.EpochClaim, CommandSourceAuthorityBinding.MethodClaim]
                : [EventStoreWorkloadAuthenticationDefaults.TenantBindingClaimType, EventStoreWorkloadAuthenticationDefaults.DomainBindingClaimType,
                    EventStoreWorkloadAuthenticationDefaults.BindingClaimPrefix + "publication-scope", EventStoreWorkloadAuthenticationDefaults.BindingClaimPrefix + "publication-method",
                    EventStoreWorkloadAuthenticationDefaults.BindingClaimPrefix + "publication-epoch"];
            return commandDigest != publicationDigest && (commandDigest ? grant.AllowCompleteCommandSourceDigest : grant.AllowSourcePublicationDigest)
                && !grant.AllowQueryAuthorityDigest && grant.Bindings.Count == required.Length && required.All(grant.Bindings.ContainsKey)
                && grant.Bindings.All(binding => binding.Value is { Length: > 0 } && binding.Value.All(value => !string.IsNullOrWhiteSpace(value) && !value.Contains('*')))
                && details.Bindings.Count == required.Length + 1 && details.Bindings.All(binding => binding.Key == dynamicClaim
                    || grant.Bindings.TryGetValue(binding.Key, out string[]? allowed) && allowed.Contains(binding.Value, StringComparer.Ordinal));
        }
        bool queryDigest = details.Bindings.ContainsKey(QueryCallerAuthorityBinding.DigestClaim);
        if (grant.AllowQueryAuthorityDigest && details.Operation == EventStoreWorkloadOperations.DomainServiceQuery && !queryDigest) return false;
        if (details.Bindings.Count == 0) return grant.AllowUnbound;
        if (queryDigest && (!grant.AllowQueryAuthorityDigest || details.Operation != EventStoreWorkloadOperations.DomainServiceQuery
            || grant.Bindings.Count != 2
            || !grant.Bindings.ContainsKey(EventStoreWorkloadAuthenticationDefaults.TenantBindingClaimType)
            || !grant.Bindings.ContainsKey(EventStoreWorkloadAuthenticationDefaults.DomainBindingClaimType)
            || grant.Bindings.Any(static binding => binding.Value is null || binding.Value.Length == 0
                || binding.Value.Any(static value => string.IsNullOrWhiteSpace(value) || value.Contains('*'))))) return false;
        return details.Bindings.Count == grant.Bindings.Count + (queryDigest ? 1 : 0)
            && details.Bindings.All(binding => queryDigest && binding.Key == QueryCallerAuthorityBinding.DigestClaim
                || grant.Bindings.TryGetValue(binding.Key, out string[]? allowed)
                    && allowed is not null && allowed.Contains(binding.Value, StringComparer.Ordinal));
    }
}
