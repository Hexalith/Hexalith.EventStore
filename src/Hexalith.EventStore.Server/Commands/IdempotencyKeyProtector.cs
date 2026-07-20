using System.Security.Cryptography;
using System.Text;

using Hexalith.EventStore.Contracts.Commands;

namespace Hexalith.EventStore.Server.Commands;

/// <summary>Derives tenant-scoped admission identity and canonical intent digests.</summary>
public sealed class IdempotencyKeyProtector(IIdempotencyDigestKeyProvider keyProvider)
{
    private static readonly byte[] _tenantDomain = "hexalith-eventstore-idempotency-tenant-v1\0"u8.ToArray();
    private static readonly byte[] _keyDomain = "key-partition-v1\0"u8.ToArray();
    private static readonly byte[] _verificationDomain = "key-verification-v1\0"u8.ToArray();
    private static readonly byte[] _intentDomain = "canonical-intent-v1\0"u8.ToArray();
    private readonly IIdempotencyDigestKeyProvider _keyProvider = keyProvider
        ?? throw new ArgumentNullException(nameof(keyProvider));

    /// <summary>Protects a raw tenant/key pair and trusted canonical descriptor.</summary>
    /// <param name="tenant">The managed tenant identifier.</param>
    /// <param name="rawKey">The caller's opaque idempotency key.</param>
    /// <param name="descriptor">The trusted canonical descriptor.</param>
    /// <returns>The active protected identity followed by retained-reader aliases.</returns>
    public async ValueTask<IdempotencyProtectedIdentitySet> ProtectAsync(
        string tenant,
        string rawKey,
        TrustedIdempotencyDescriptor descriptor,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        ArgumentException.ThrowIfNullOrWhiteSpace(rawKey);
        ArgumentNullException.ThrowIfNull(descriptor);

        if (string.IsNullOrWhiteSpace(descriptor.AdapterId)
            || string.IsNullOrWhiteSpace(descriptor.OperationId)
            || descriptor.DescriptorVersion <= 0
            || !Enum.IsDefined(descriptor.RetentionTier))
        {
            throw new InvalidOperationException("The trusted canonical idempotency descriptor is invalid.");
        }

        if (descriptor.CanonicalIntent is null || descriptor.CanonicalIntent.Length == 0)
        {
            throw new ArgumentException("Canonical intent bytes are required.", nameof(descriptor));
        }

        using IdempotencyDigestKeyRing keyRing = await _keyProvider
            .GetKeyRingAsync(cancellationToken)
            .ConfigureAwait(false);
        var aliases = new List<IdempotencyProtectedIdentity>(keyRing.Versions.Count);
        foreach (string version in keyRing.Versions)
        {
            byte[] masterKey = keyRing.RentKeyMaterial(version);
            byte[]? tenantBytes = null;
            byte[]? tenantKey = null;
            byte[]? rawKeyBytes = null;
            try
            {
                tenantBytes = Encoding.UTF8.GetBytes(tenant);
                tenantKey = ComputeHmac(masterKey, _tenantDomain, tenantBytes);
                rawKeyBytes = Encoding.UTF8.GetBytes(rawKey);
                string keyDigest = Base64Url(ComputeHmac(tenantKey, _keyDomain, rawKeyBytes));
                string verificationTag = Base64Url(ComputeHmac(tenantKey, _verificationDomain, rawKeyBytes));
                string intentDigest = Base64Url(ComputeHmac(tenantKey, _intentDomain, descriptor.CanonicalIntent));
                string actorId = string.Concat(tenant, ":", version, ":", keyDigest);
                aliases.Add(new IdempotencyProtectedIdentity(
                    actorId,
                    tenant,
                    version,
                    keyDigest,
                    verificationTag,
                    intentDigest,
                    descriptor.RetentionTier));
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
        }

        return new IdempotencyProtectedIdentitySet(aliases[0], aliases.AsReadOnly());
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
