using System.Buffers.Binary;
using System.Text;

namespace Hexalith.EventStore.Sharding.Tests;

/// <summary>Strict bounded decoder for the supported plaintext extended-protocol messages.</summary>
/// <param name="payload">One complete frame payload.</param>
internal sealed class PostgresPayloadReader(byte[] payload)
{
    private static readonly UTF8Encoding Utf8 = new(false, true);
    private int _offset;
    /// <summary>Reads a strictly terminated UTF-8 field.</summary>
    internal string String()
    {
        int end = Array.IndexOf(payload, (byte)0, _offset);
        if (end < 0) throw new InvalidDataException("Unterminated PostgreSQL protocol field.");
        string value = Decode(payload.AsSpan(_offset, end - _offset));
        _offset = end + 1;
        return value;
    }
    /// <summary>Reads a network-order count or format.</summary>
    internal short Int16() => BinaryPrimitives.ReadInt16BigEndian(Bytes(2));
    /// <summary>Reads a network-order length or integer.</summary>
    internal int Int32() => BinaryPrimitives.ReadInt32BigEndian(Bytes(4));
    /// <summary>Reads a bounded exact byte sequence.</summary>
    internal byte[] Bytes(int count)
    {
        if (count < 0 || count > payload.Length - _offset) throw new InvalidDataException("Truncated PostgreSQL protocol field.");
        byte[] result = payload.AsSpan(_offset, count).ToArray();
        _offset += count;
        return result;
    }
    /// <summary>Rejects unsupported trailing fields.</summary>
    internal void End()
    {
        if (_offset != payload.Length) throw new InvalidDataException("Unexpected PostgreSQL protocol trailing bytes.");
    }
    /// <summary>Decodes UTF-8 without replacing malformed bytes.</summary>
    internal static string Decode(ReadOnlySpan<byte> bytes)
    {
        try { return Utf8.GetString(bytes); }
        catch (DecoderFallbackException exception) { throw new InvalidDataException("Invalid PostgreSQL UTF-8.", exception); }
    }
}
