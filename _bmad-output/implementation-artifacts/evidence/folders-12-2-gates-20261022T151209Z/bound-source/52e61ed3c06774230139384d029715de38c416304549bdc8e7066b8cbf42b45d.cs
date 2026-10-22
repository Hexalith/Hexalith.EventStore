using System.Buffers.Binary;
using System.Text;

namespace Hexalith.EventStore.Client.Events;

/// <summary>Reads bounded strict big-endian evidence without copying length-prefixed byte fields.</summary>
internal ref struct EventEvolutionBinaryReader
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private readonly ReadOnlySpan<byte> _bytes;
    private int _position;

    /// <summary>Uses an already admitted immutable bounded record.</summary>
    internal EventEvolutionBinaryReader(ReadOnlySpan<byte> bytes)
    {
        _bytes = bytes;
        _position = 0;
    }

    /// <summary>Gets the consumed byte count.</summary>
    internal readonly int Position => _position;

    /// <summary>Reads one field tag or discriminator.</summary>
    internal byte ReadByte() => ReadRaw(1)[0];

    /// <summary>Reads an unsigned field count.</summary>
    internal ushort ReadUInt16() => BinaryPrimitives.ReadUInt16BigEndian(ReadRaw(2));

    /// <summary>Reads an unsigned byte length or collection count.</summary>
    internal uint ReadUInt32() => BinaryPrimitives.ReadUInt32BigEndian(ReadRaw(4));

    /// <summary>Reads the signed I primitive.</summary>
    internal int ReadInt32() => BinaryPrimitives.ReadInt32BigEndian(ReadRaw(4));

    /// <summary>Reads the signed N primitive.</summary>
    internal long ReadInt64() => BinaryPrimitives.ReadInt64BigEndian(ReadRaw(8));

    /// <summary>Reads strict UTF-8 only after checking its encoded length (U).</summary>
    internal string ReadString(int maximumBytes)
    {
        ReadOnlySpan<byte> value = ReadBytes(maximumBytes);
        return StrictUtf8.GetString(value);
    }

    /// <summary>Reads B as a private read-only slice after checking the prefix and remaining capacity.</summary>
    internal ReadOnlySpan<byte> ReadBytes(int maximumBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maximumBytes);
        uint length = ReadUInt32();
        if (length > maximumBytes)
        {
            throw new ArgumentException("A counted evidence field exceeds its admitted byte limit.");
        }

        return ReadRaw(checked((int)length));
    }

    /// <summary>Reads exactly 32 unframed bytes (B32).</summary>
    internal ReadOnlySpan<byte> ReadHash() => ReadRaw(32);

    /// <summary>Reads UTC ticks and the exact original offset, rejecting unrepresentable T values.</summary>
    internal DateTimeOffset ReadTimestamp()
    {
        long ticks = ReadInt64();
        short minutes = BinaryPrimitives.ReadInt16BigEndian(ReadRaw(2));
        if (minutes is < -840 or > 840)
        {
            throw new ArgumentException("A timestamp offset must be between -840 and 840 minutes.");
        }

        return new DateTimeOffset(ticks, TimeSpan.Zero).ToOffset(TimeSpan.FromMinutes(minutes));
    }

    /// <summary>Reads a UTC-only instant (Q).</summary>
    internal DateTimeOffset ReadInstant() => new(ReadInt64(), TimeSpan.Zero);

    /// <summary>Rejects unexpected trailing bytes.</summary>
    internal readonly void RequireEnd()
    {
        if (_position != _bytes.Length)
        {
            throw new ArgumentException("Trailing bytes are not permitted in an evidence record.");
        }
    }

    /// <summary>Consumes an exact fixed length without copying or exposing a writable buffer.</summary>
    internal ReadOnlySpan<byte> ReadRaw(int length)
    {
        if (length < 0 || length > _bytes.Length - _position)
        {
            throw new ArgumentException("Truncated evidence record.");
        }

        ReadOnlySpan<byte> result = _bytes.Slice(_position, length);
        _position += length;
        return result;
    }
}
