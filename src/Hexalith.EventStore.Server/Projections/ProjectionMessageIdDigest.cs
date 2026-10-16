using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>An exact SHA-256 value key for complete identity membership without retaining Base64 strings.</summary>
internal readonly record struct ProjectionMessageIdDigest(ulong First, ulong Second, ulong Third, ulong Fourth)
{
    internal static bool TryParse(string? value, out ProjectionMessageIdDigest digest)
    {
        Span<byte> bytes = stackalloc byte[SHA256.HashSizeInBytes];
        if (value is null || !Convert.TryFromBase64String(value, bytes, out int written)
            || written != bytes.Length || Convert.ToBase64String(bytes) != value)
        {
            digest = default;
            return false;
        }
        digest = FromBytes(bytes);
        return true;
    }

    internal static ProjectionMessageIdDigest FromMessageId(string messageId)
    {
        Span<byte> bytes = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(Encoding.UTF8.GetBytes(messageId), bytes);
        return FromBytes(bytes);
    }

    private static ProjectionMessageIdDigest FromBytes(ReadOnlySpan<byte> bytes) => new(
        BinaryPrimitives.ReadUInt64LittleEndian(bytes), BinaryPrimitives.ReadUInt64LittleEndian(bytes[8..]),
        BinaryPrimitives.ReadUInt64LittleEndian(bytes[16..]), BinaryPrimitives.ReadUInt64LittleEndian(bytes[24..]));
}
