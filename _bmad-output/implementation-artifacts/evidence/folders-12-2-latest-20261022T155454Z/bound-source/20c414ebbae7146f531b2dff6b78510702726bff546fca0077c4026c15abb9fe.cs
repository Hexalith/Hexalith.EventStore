using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using ByteAether.Ulid;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Streams;

namespace Hexalith.EventStore.Client.Streams;

/// <summary>The existing source-v1 seed, step and metadata validation shared with the native server codec.</summary>
public static class NativeSourceProofCodec
{
    /// <summary>The supported source codec version.</summary>
    public const int Version = 1;
    /// <summary>Creates the scoped chain seed for a new stream incarnation.</summary>
    public static NativeSourceProof CreateSeed(AggregateIdentity identity, string incarnation)
    {
        ArgumentNullException.ThrowIfNull(identity);
        if (!Ulid.TryParse(incarnation, null, out _))
        {
            throw new ArgumentException("The source incarnation must be a ULID.", nameof(incarnation));
        }

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendString(hash, "hexalith.projection.source.seed.v1");
        AppendString(hash, identity.TenantId);
        AppendString(hash, identity.Domain);
        AppendString(hash, identity.AggregateId);
        AppendString(hash, incarnation);
        return new(Version, identity.TenantId, identity.Domain, identity.AggregateId,
            incarnation, 0, null, Convert.ToHexStringLower(hash.GetHashAndReset()), 1, false);
    }

    /// <summary>Extends using an already measured raw-envelope digest, avoiding a duplicate source hash.</summary>
    public static NativeSourceProof ExtendDigest(NativeSourceProof previous, AggregateIdentity identity, long sequence, string messageId, string eventDigest)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(identity);
        if (!IsValid(previous, identity, previous.HeadSequence, previous.RetainedFloor)
            || previous.Erased || sequence != checked(previous.HeadSequence + 1)
            || string.IsNullOrWhiteSpace(messageId) || !IsDigest(eventDigest))
        {
            throw new ArgumentException("The source envelope does not extend the proven contiguous stream.", nameof(sequence));
        }

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendString(hash, "hexalith.projection.source.step.v1");
        hash.AppendData(Convert.FromHexString(previous.CumulativeDigest));
        hash.AppendData(Convert.FromHexString(eventDigest));
        return previous with
        {
            HeadSequence = sequence,
            HeadMessageId = messageId,
            CumulativeDigest = Convert.ToHexStringLower(hash.GetHashAndReset()),
        };
    }

    /// <summary>Validates metadata evidence without reading any event envelope.</summary>
    public static bool IsValid(NativeSourceProof proof, AggregateIdentity identity, long head, long floor)
    {
        ArgumentNullException.ThrowIfNull(proof);
        ArgumentNullException.ThrowIfNull(identity);
        return proof.CodecVersion == Version && proof.HeadSequence == head && head >= 0
            && floor >= 1 && floor - 1 <= head && proof.RetainedFloor == floor
            && string.Equals(proof.TenantId, identity.TenantId, StringComparison.Ordinal)
            && string.Equals(proof.Domain, identity.Domain, StringComparison.Ordinal)
            && string.Equals(proof.AggregateId, identity.AggregateId, StringComparison.Ordinal)
            && Ulid.TryParse(proof.SourceIncarnation, null, out _)
            && IsDigest(proof.CumulativeDigest)
            && (head == 0 ? proof.HeadMessageId is null
                && proof.CumulativeDigest == CreateSeed(identity, proof.SourceIncarnation).CumulativeDigest
                : !string.IsNullOrWhiteSpace(proof.HeadMessageId));
    }

    private static bool IsDigest(string? digest)
        => digest is { Length: 64 } && digest.All(static character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static void AppendString(IncrementalHash hash, string? value)
    {
        if (value is null)
        {
            AppendInt32(hash, -1);
            return;
        }

        AppendBytes(hash, Encoding.UTF8.GetBytes(value));
    }

    private static void AppendBytes(IncrementalHash hash, byte[] value)
    {
        AppendInt32(hash, value.Length);
        hash.AppendData(value);
    }

    private static void AppendInt32(IncrementalHash hash, int value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, value);
        hash.AppendData(bytes);
    }

    private static void AppendInt64(IncrementalHash hash, long value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(long)];
        BinaryPrimitives.WriteInt64LittleEndian(bytes, value);
        hash.AppendData(bytes);
    }
}
