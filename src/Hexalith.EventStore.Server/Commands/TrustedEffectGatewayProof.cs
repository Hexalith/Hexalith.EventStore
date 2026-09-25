using System.Buffers;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

using Hexalith.EventStore.Contracts.Effects;

namespace Hexalith.EventStore.Server.Commands;

/// <summary>Domain-separated HMAC proof scoped to the complete admitted effect and gateway identity.</summary>
public sealed class TrustedEffectGatewayProof(IIdempotencyDigestKeyProvider keyProvider)
    : ITrustedEffectGatewayProof
{
    private static readonly byte[] _domain = "hexalith-trusted-effect-gateway-proof-v1\0"u8.ToArray();

    /// <inheritdoc/>
    public async Task<string> SignAsync(TrustedEffectAdmission admission, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(admission);
        using IdempotencyDigestKeyRing ring = await keyProvider.GetKeyRingAsync(cancellationToken)
            .ConfigureAwait(false);
        byte[] key = ring.RentKeyMaterial(ring.ActiveVersion);
        try
        {
            string tag = Convert.ToHexString(HMACSHA256.HashData(key, Encode(admission)));
            return ring.ActiveVersion + "." + tag;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    /// <inheritdoc/>
    public async Task ValidateAsync(
        TrustedEffectAdmission admission,
        string proof,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(admission);
        ArgumentException.ThrowIfNullOrWhiteSpace(proof);
        int separator = proof.IndexOf('.');
        if (separator <= 0 || separator == proof.Length - 1 || proof[(separator + 1)..].Length != 64)
        {
            throw new InvalidOperationException("Trusted effect gateway proof is invalid.");
        }

        string version = proof[..separator];
        using IdempotencyDigestKeyRing ring = await keyProvider.GetKeyRingAsync(cancellationToken)
            .ConfigureAwait(false);
        if (!ring.Versions.Contains(version, StringComparer.Ordinal))
        {
            throw new InvalidOperationException("Trusted effect gateway proof key is unavailable.");
        }

        byte[] key = ring.RentKeyMaterial(version);
        try
        {
            byte[] expected = HMACSHA256.HashData(key, Encode(admission));
            byte[] actual;
            try
            {
                actual = Convert.FromHexString(proof[(separator + 1)..]);
            }
            catch (FormatException)
            {
                throw new InvalidOperationException("Trusted effect gateway proof is invalid.");
            }

            if (!CryptographicOperations.FixedTimeEquals(expected, actual))
            {
                throw new InvalidOperationException("Trusted effect gateway proof does not match admission.");
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    private static byte[] Encode(TrustedEffectAdmission admission)
    {
        TrustedEffectSubmission submission = admission.Submission;
        var output = new ArrayBufferWriter<byte>();
        Write(output, _domain);
        Write(output, EffectIdentityCodec.Encode(submission.Identity));
        Write(output, Encoding.UTF8.GetBytes(submission.CommandType));
        Write(output, Encoding.UTF8.GetBytes(submission.MessageId));
        Write(output, Encoding.UTF8.GetBytes(admission.SemanticDigest));
        Write(output, Encoding.UTF8.GetBytes(admission.Context.Workload));
        Write(output, Encoding.UTF8.GetBytes(admission.Context.Purpose));
        Write(output, Encoding.UTF8.GetBytes(admission.Context.CausationId));
        Write(output, SHA256.HashData(Encoding.UTF8.GetBytes(admission.Context.DelegationToken)));
        return output.WrittenSpan.ToArray();
    }

    private static void Write(ArrayBufferWriter<byte> output, ReadOnlySpan<byte> value)
    {
        Span<byte> target = output.GetSpan(sizeof(int) + value.Length);
        BinaryPrimitives.WriteInt32BigEndian(target, value.Length);
        value.CopyTo(target[sizeof(int)..]);
        output.Advance(sizeof(int) + value.Length);
    }
}
