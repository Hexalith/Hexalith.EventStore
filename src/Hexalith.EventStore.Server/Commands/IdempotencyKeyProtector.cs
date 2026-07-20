using System.Security.Cryptography;
using System.Text;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Server.Configuration;

using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Server.Commands;

/// <summary>Derives tenant-scoped admission identity and canonical intent digests.</summary>
public sealed class IdempotencyKeyProtector(IOptions<IdempotencyAdmissionOptions> options)
{
    private static readonly byte[] _tenantDomain = "hexalith-eventstore-idempotency-tenant-v1\0"u8.ToArray();
    private static readonly byte[] _keyDomain = "key-partition-v1\0"u8.ToArray();
    private static readonly byte[] _verificationDomain = "key-verification-v1\0"u8.ToArray();
    private static readonly byte[] _intentDomain = "canonical-intent-v1\0"u8.ToArray();
    private readonly IdempotencyAdmissionOptions _options = options?.Value
        ?? throw new ArgumentNullException(nameof(options));

    /// <summary>Protects a raw tenant/key pair and trusted canonical descriptor.</summary>
    /// <param name="tenant">The managed tenant identifier.</param>
    /// <param name="rawKey">The caller's opaque idempotency key.</param>
    /// <param name="descriptor">The trusted canonical descriptor.</param>
    /// <returns>Protected actor-routing and comparison material.</returns>
    public IdempotencyProtectedIdentity Protect(
        string tenant,
        string rawKey,
        CanonicalIdempotencyDescriptor descriptor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        ArgumentException.ThrowIfNullOrWhiteSpace(rawKey);
        ArgumentNullException.ThrowIfNull(descriptor);

        if (!_options.Enabled)
        {
            throw new InvalidOperationException("Trusted idempotency admission is not enabled.");
        }

        string policyKey = string.Concat(descriptor.AdapterId, ":", descriptor.OperationId);
        if (!_options.Operations.TryGetValue(policyKey, out IdempotencyAdmissionOperationOptions? policy)
            || policy.DescriptorVersion != descriptor.DescriptorVersion
            || policy.RetentionTier != descriptor.RetentionTier)
        {
            throw new InvalidOperationException("The canonical idempotency descriptor is not registered.");
        }

        if (descriptor.CanonicalIntent is null || descriptor.CanonicalIntent.Length == 0)
        {
            throw new ArgumentException("Canonical intent bytes are required.", nameof(descriptor));
        }

        if (!_options.DigestKeys.TryGetValue(_options.ActiveDigestKeyVersion, out string? encodedMasterKey))
        {
            throw new InvalidOperationException("The active idempotency digest key is unavailable.");
        }

        byte[] masterKey;
        try
        {
            masterKey = Convert.FromBase64String(encodedMasterKey);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException("The active idempotency digest key is invalid.", ex);
        }

        byte[]? tenantBytes = null;
        byte[]? tenantKey = null;
        byte[]? rawKeyBytes = null;
        string keyDigest;
        string verificationTag;
        string intentDigest;
        try
        {
            if (masterKey.Length < 32)
            {
                throw new InvalidOperationException("The active idempotency digest key is too short.");
            }

            tenantBytes = Encoding.UTF8.GetBytes(tenant);
            tenantKey = ComputeHmac(masterKey, _tenantDomain, tenantBytes);
            rawKeyBytes = Encoding.UTF8.GetBytes(rawKey);
            keyDigest = Base64Url(ComputeHmac(tenantKey, _keyDomain, rawKeyBytes));
            verificationTag = Base64Url(ComputeHmac(tenantKey, _verificationDomain, rawKeyBytes));
            intentDigest = Base64Url(ComputeHmac(tenantKey, _intentDomain, descriptor.CanonicalIntent));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(masterKey);
            if (tenantBytes is not null)
            {
                CryptographicOperations.ZeroMemory(tenantBytes);
            }

            if (tenantKey is not null)
            {
                CryptographicOperations.ZeroMemory(tenantKey);
            }

            if (rawKeyBytes is not null)
            {
                CryptographicOperations.ZeroMemory(rawKeyBytes);
            }
        }

        string actorId = string.Concat(tenant, ":", _options.ActiveDigestKeyVersion, ":", keyDigest);
        return new IdempotencyProtectedIdentity(
            actorId,
            tenant,
            _options.ActiveDigestKeyVersion,
            keyDigest,
            verificationTag,
            intentDigest,
            descriptor.RetentionTier);
    }

    private static byte[] ComputeHmac(byte[] key, byte[] domain, byte[] value)
    {
        byte[] input = new byte[domain.Length + value.Length];
        Buffer.BlockCopy(domain, 0, input, 0, domain.Length);
        Buffer.BlockCopy(value, 0, input, domain.Length, value.Length);
        byte[] digest = HMACSHA256.HashData(key, input);
        CryptographicOperations.ZeroMemory(input);
        return digest;
    }

    private static string Base64Url(byte[] value)
        => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
