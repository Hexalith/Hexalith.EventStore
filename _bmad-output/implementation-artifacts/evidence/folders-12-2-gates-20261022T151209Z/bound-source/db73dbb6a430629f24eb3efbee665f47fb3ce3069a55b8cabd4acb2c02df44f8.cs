using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Hexalith.EventStore.Client.Events;

/// <summary>Writes the approved big-endian evidence primitives into one prebounded private owner.</summary>
internal sealed class EventEvolutionBinaryWriter : IDisposable
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private byte[]? _owner;
    private int _length;

    /// <summary>Reserves an exact capacity before any framed evidence is written.</summary>
    internal EventEvolutionBinaryWriter(int maximumBytes)
    {
        if (maximumBytes is < 0 or > 64 * 1024 * 1024)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumBytes));
        }

        _owner = new byte[maximumBytes];
    }

    /// <summary>Gets the number of encoded bytes.</summary>
    internal int Length => _length;

    /// <summary>Writes one raw byte, including a field tag or presence discriminator.</summary>
    internal void WriteByte(byte value) => Reserve(1)[0] = value;

    /// <summary>Writes an unsigned big-endian field count.</summary>
    internal void WriteUInt16(ushort value) => BinaryPrimitives.WriteUInt16BigEndian(Reserve(2), value);

    /// <summary>Writes an unsigned big-endian byte length or collection count.</summary>
    internal void WriteUInt32(uint value) => BinaryPrimitives.WriteUInt32BigEndian(Reserve(4), value);

    /// <summary>Writes the signed big-endian I primitive.</summary>
    internal void WriteInt32(int value) => BinaryPrimitives.WriteInt32BigEndian(Reserve(4), value);

    /// <summary>Writes the signed big-endian N primitive.</summary>
    internal void WriteInt64(long value) => BinaryPrimitives.WriteInt64BigEndian(Reserve(8), value);

    /// <summary>Writes strict UTF-8 with its checked unsigned byte length (U).</summary>
    internal void WriteString(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        int byteCount = StrictUtf8.GetByteCount(value);
        Span<byte> destination = Reserve(checked(4 + byteCount));
        BinaryPrimitives.WriteUInt32BigEndian(destination, checked((uint)byteCount));
        _ = StrictUtf8.GetBytes(value, destination[4..]);
    }

    /// <summary>Writes a checked unsigned byte length followed by its bytes (B).</summary>
    internal void WriteBytes(ReadOnlySpan<byte> value)
    {
        Span<byte> destination = Reserve(checked(4 + value.Length));
        BinaryPrimitives.WriteUInt32BigEndian(destination, checked((uint)value.Length));
        value.CopyTo(destination[4..]);
    }

    /// <summary>Writes exactly 32 unframed hash bytes (B32).</summary>
    internal void WriteHash(ReadOnlySpan<byte> value)
    {
        if (value.Length != 32)
        {
            throw new ArgumentException("A B32 field must contain exactly 32 bytes.", nameof(value));
        }

        WriteRaw(value);
    }

    /// <summary>Writes UTC ticks and the original signed offset minutes (T).</summary>
    internal void WriteTimestamp(DateTimeOffset value)
    {
        Span<byte> destination = Reserve(10);
        BinaryPrimitives.WriteInt64BigEndian(destination, value.UtcTicks);
        BinaryPrimitives.WriteInt16BigEndian(destination[8..], checked((short)value.Offset.TotalMinutes));
    }

    /// <summary>Writes UTC instant ticks without an offset (Q).</summary>
    internal void WriteInstant(DateTimeOffset value) => WriteInt64(value.UtcTicks);

    /// <summary>Writes exact preframed bytes, such as an ASCII separator or validated row.</summary>
    internal void WriteRaw(ReadOnlySpan<byte> value) => value.CopyTo(Reserve(value.Length));

    /// <summary>Returns a detached transport copy of only the encoded bytes.</summary>
    internal byte[] CopyEncodedBytes()
    {
        ObjectDisposedException.ThrowIf(_owner is null, this);
        return _owner.AsSpan(0, _length).ToArray();
    }

    /// <summary>Hashes the encoded bytes without creating a second full copy.</summary>
    internal byte[] ComputeSha256()
    {
        ObjectDisposedException.ThrowIf(_owner is null, this);
        return SHA256.HashData(_owner.AsSpan(0, _length));
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        byte[]? owner = Interlocked.Exchange(ref _owner, null);
        if (owner is not null)
        {
            CryptographicOperations.ZeroMemory(owner);
        }
    }

    private Span<byte> Reserve(int count)
    {
        ObjectDisposedException.ThrowIf(_owner is null, this);
        if (count < 0 || count > _owner.Length - _length)
        {
            throw new InvalidOperationException("RegistryLimit: encoded evidence exceeds its preallocated capacity.");
        }

        Span<byte> result = _owner.AsSpan(_length, count);
        _length += count;
        return result;
    }
}
