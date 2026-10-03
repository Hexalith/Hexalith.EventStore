using System.Security.Cryptography;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Security;

using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Client.Security;

/// <summary>Public-key verification of domain-separated admission with exact payload, tenant, operation and target binding.</summary>
public sealed class IdentityAdmissionProof(IOptionsMonitor<IdentityAdmissionOptions> options, TimeProvider timeProvider)
    : IIdentityAdmissionProof
{
    /// <summary>The gateway-owned extension namespace; public callers may never provide it.</summary>
    public const string ExtensionKey = "identity:admission";

    private static readonly byte[] Domain = "hexalith-identity-admission-v1\0"u8.ToArray();

    /// <inheritdoc/>
    public IdentityAdmissionEvidence? Verify(string? proof, IdentityAdmissionScope expected)
    {
        ArgumentNullException.ThrowIfNull(expected);
        if (string.IsNullOrWhiteSpace(proof) || proof.Length > 8192)
        {
            return null;
        }

        try
        {
            int separator = proof.IndexOf('.');
            if (separator <= 0 || proof.Length - separator - 1 < 256)
            {
                return null;
            }

            byte[] bytes = Convert.FromBase64String(proof[..separator]);
            byte[] signature = Convert.FromBase64String(proof[(separator + 1)..]);
            IdentityAdmissionOptions configured = options.CurrentValue;
            string publicKey = configured.VerificationKeyPem
                ?? throw new InvalidOperationException("Identity admission is not configured.");
            if (publicKey.Contains("PRIVATE KEY", StringComparison.Ordinal))
            {
                return null;
            }

            using RSA rsa = RSA.Create();
            rsa.ImportFromPem(publicKey);
            if (rsa.KeySize < 2048 || !rsa.VerifyData(Encode(bytes), signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pss))
            {
                return null;
            }

            IdentityAdmissionEvidence? evidence = JsonSerializer.Deserialize<IdentityAdmissionEvidence>(bytes);
            DateTimeOffset now = timeProvider.GetUtcNow();
            return evidence?.Scope == expected && evidence.AuthorityRevision == configured.AuthorityRevision
                && evidence.AuthorityRevision > 0 && !string.IsNullOrWhiteSpace(evidence.SourceId)
                && evidence.IssuedAt <= now && now < evidence.ExpiresAt
                && evidence.ExpiresAt - evidence.IssuedAt <= TimeSpan.FromMinutes(5)
                    ? evidence : null;
        }
        catch (Exception exception) when (exception is FormatException or JsonException or InvalidOperationException or ArgumentException or CryptographicException)
        {
            return null;
        }
    }

    /// <summary>Computes the domain admission payload digest without exposing payload content.</summary>
    public static string Digest(byte[] payload) => Convert.ToHexString(SHA256.HashData(payload));

    /// <summary>Encodes the fixed domain-separated admission bytes for the trusted signer.</summary>
    public static byte[] Encode(byte[] evidenceBytes)
    {
        ArgumentNullException.ThrowIfNull(evidenceBytes);
        return Domain.Concat(evidenceBytes).ToArray();
    }
}
