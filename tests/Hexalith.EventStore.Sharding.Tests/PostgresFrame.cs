using System.Buffers.Binary;
using System.Text;

namespace Hexalith.EventStore.Sharding.Tests;

/// <summary>A bounded PostgreSQL protocol frame; startup/authentication payloads are never logged.</summary>
/// <param name="Type">The typed protocol message, or zero for startup.</param>
/// <param name="Payload">The exact payload bytes.</param>
internal sealed record PostgresFrame(byte Type, byte[] Payload)
{
    private const int MaximumLength = 16 * 1024 * 1024;

    /// <summary>Reads an entire frame despite fragmented TCP reads; clean EOF returns null.</summary>
    internal static async Task<PostgresFrame?> ReadAsync(Stream stream, bool startup, CancellationToken token)
    {
        byte[] header = new byte[startup ? 4 : 5];
        int first = await stream.ReadAsync(header.AsMemory(0, 1), token);
        if (first == 0) return null;
        await stream.ReadExactlyAsync(header.AsMemory(1), token);
        int length = BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(startup ? 0 : 1));
        if (length < 4 || length > MaximumLength) throw new InvalidDataException("Invalid PostgreSQL frame length.");
        byte[] payload = new byte[length - 4];
        await stream.ReadExactlyAsync(payload, token);
        return new(startup ? (byte)0 : header[0], payload);
    }

    /// <summary>Writes the exact frame without inspecting authentication bytes.</summary>
    internal async Task WriteAsync(Stream stream, CancellationToken token)
    {
        byte[] header = new byte[Type == 0 ? 4 : 5];
        if (Type != 0) header[0] = Type;
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(Type == 0 ? 0 : 1), checked(Payload.Length + 4));
        await stream.WriteAsync(header, token);
        await stream.WriteAsync(Payload, token);
    }

    /// <summary>Gets a zero-terminated UTF-8 protocol field.</summary>
    internal string Field(int offset = 0)
    {
        int end = Array.IndexOf(Payload, (byte)0, offset);
        if (end < 0) throw new InvalidDataException("Unterminated PostgreSQL field.");
        return Encoding.UTF8.GetString(Payload, offset, end - offset);
    }

    /// <summary>Recognizes an exact NOTICE message field, rather than arbitrary SQL/authentication bytes.</summary>
    internal bool HasNotice(string marker)
    {
        if (Type != (byte)'N') return false;
        for (int offset = 0; offset < Payload.Length && Payload[offset] != 0;)
        {
            byte code = Payload[offset++];
            string value = Field(offset);
            if (code == (byte)'M' && value == marker) return true;
            offset = Array.IndexOf(Payload, (byte)0, offset) + 1;
        }

        return false;
    }
}
