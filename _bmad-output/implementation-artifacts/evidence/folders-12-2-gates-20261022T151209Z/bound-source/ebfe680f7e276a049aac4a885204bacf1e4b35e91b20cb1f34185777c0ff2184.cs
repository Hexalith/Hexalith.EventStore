using System.Buffers;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

using Hexalith.EventStore.Contracts.Identity;

namespace Hexalith.EventStore.Contracts.Effects;

/// <summary>Version one of the canonical AD-26 effect identity encoding.</summary>
public static class EffectIdentityCodec
{
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    /// <summary>Gets the immutable wire codec version.</summary>
    public const int Version = 1;

    /// <summary>Encodes the tuple without a version prefix; its field order is the version-one contract.</summary>
    public static byte[] Encode(EffectIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);
        if (identity.SourceEnvelopeSequence <= 0 || identity.Ordinal < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(identity), "Source sequence must be positive and ordinal nonnegative.");
        }

        var source = new AggregateIdentity(identity.Tenant, identity.SourceDomain, identity.SourceAggregate);
        var target = new AggregateIdentity(identity.Tenant, identity.TargetDomain, identity.TargetAggregate);
        if (!string.Equals(identity.Tenant, source.TenantId, StringComparison.Ordinal)
            || !string.Equals(identity.SourceDomain, source.Domain, StringComparison.Ordinal)
            || !string.Equals(identity.TargetDomain, target.Domain, StringComparison.Ordinal))
        {
            throw new ArgumentException("Effect identity tenant and domains must be canonical lowercase.", nameof(identity));
        }

        if (!EffectKindCatalog.IsKnown(identity.EffectKind))
        {
            throw new ArgumentException("Effect kind is not in the version-one catalog.", nameof(identity));
        }

        var buffer = new ArrayBufferWriter<byte>();
        WriteText(buffer, identity.Tenant);
        WriteText(buffer, identity.SourceDomain);
        WriteText(buffer, identity.SourceAggregate);
        WriteLong(buffer, identity.SourceEnvelopeSequence);
        WriteText(buffer, identity.EffectKind);
        WriteText(buffer, identity.TargetDomain);
        WriteText(buffer, identity.TargetAggregate);
        WriteLong(buffer, identity.Ordinal);
        return buffer.WrittenSpan.ToArray();
    }

    /// <summary>Computes a 52-character uppercase Crockford Base32 SHA-256 identifier.</summary>
    public static string ComputeEffectId(EffectIdentity identity)
        => RenderDigest(SHA256.HashData(Encode(identity)));

    /// <summary>Computes the gateway-safe message and idempotency key.</summary>
    public static string ComputeMessageId(EffectIdentity identity)
        => "wrk-" + ComputeEffectId(identity);

    /// <summary>Renders a SHA-256 digest most-significant-bit first with zero padding.</summary>
    public static string RenderDigest(ReadOnlySpan<byte> digest)
    {
        if (digest.Length != 32)
        {
            throw new ArgumentException("A SHA-256 digest must contain 32 bytes.", nameof(digest));
        }

        Span<char> result = stackalloc char[52];
        for (int group = 0; group < result.Length; group++)
        {
            int value = 0;
            for (int bit = 0; bit < 5; bit++)
            {
                int position = (group * 5) + bit;
                value <<= 1;
                if (position < 256)
                {
                    value |= (digest[position / 8] >> (7 - (position % 8))) & 1;
                }
            }

            result[group] = Alphabet[value];
        }

        return new string(result);
    }

    private static void WriteText(ArrayBufferWriter<byte> buffer, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (!string.Equals(value, value.Normalize(NormalizationForm.FormC), StringComparison.Ordinal))
        {
            throw new ArgumentException("Effect identity text must be canonical NFC.", nameof(value));
        }

        byte[] bytes = new UTF8Encoding(false, true).GetBytes(value);
        Span<byte> target = buffer.GetSpan(sizeof(int) + bytes.Length);
        BinaryPrimitives.WriteInt32BigEndian(target, bytes.Length);
        bytes.CopyTo(target[sizeof(int)..]);
        buffer.Advance(sizeof(int) + bytes.Length);
    }

    private static void WriteLong(ArrayBufferWriter<byte> buffer, long value)
    {
        Span<byte> target = buffer.GetSpan(sizeof(long));
        BinaryPrimitives.WriteInt64BigEndian(target, value);
        buffer.Advance(sizeof(long));
    }
}
