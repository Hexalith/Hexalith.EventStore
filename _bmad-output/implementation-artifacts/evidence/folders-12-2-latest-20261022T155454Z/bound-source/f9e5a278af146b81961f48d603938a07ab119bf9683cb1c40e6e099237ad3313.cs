using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Hexalith.EventStore.Client.Projections;

/// <summary>Computes the identity of one captured, owner-sequenced event delivery.</summary>
internal static class SharedProjectionCapturedEventIdentity
{
    /// <summary>Hashes the canonical event payload together with its source stream and positions.</summary>
    internal static byte[] Compute(
        string sourceStream,
        long envelopePosition,
        long ownerSequenceNumber,
        ReadOnlySpan<byte> canonicalPayload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceStream);
        if (envelopePosition <= 0 || ownerSequenceNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(envelopePosition),
                "Captured events require positive source position and owner sequence.");
        }

        byte[] stream = Encoding.UTF8.GetBytes(sourceStream);
        Span<byte> header = stackalloc byte[sizeof(int) + sizeof(long) + sizeof(long) + sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(header, stream.Length);
        BinaryPrimitives.WriteInt64LittleEndian(header[sizeof(int)..], envelopePosition);
        BinaryPrimitives.WriteInt64LittleEndian(header[(sizeof(int) + sizeof(long))..], ownerSequenceNumber);
        BinaryPrimitives.WriteInt32LittleEndian(header[(sizeof(int) + sizeof(long) * 2)..], canonicalPayload.Length);

        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(header);
        hash.AppendData(stream);
        hash.AppendData(canonicalPayload);
        return hash.GetHashAndReset();
    }
}
