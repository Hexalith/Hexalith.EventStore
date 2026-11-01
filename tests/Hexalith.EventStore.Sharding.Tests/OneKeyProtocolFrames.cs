using System.Buffers.Binary;
using System.Text;

namespace Hexalith.EventStore.Sharding.Tests;

/// <summary>Constructs actual extended-protocol test inputs with bounded length-prefixed parameters.</summary>
internal static class OneKeyProtocolFrames
{
    /// <summary>Creates a prepared provider write, including its parameter type count.</summary>
    internal static PostgresFrame Parse(string statement = "statement", string? sql = null)
        => new((byte)'P', Encoding.UTF8.GetBytes(statement + "\0" + (sql ?? PostgresOneKeySession.InsertSql) + "\0\0\0"));

    /// <summary>Creates a full text or binary parameter binding, with optional etag.</summary>
    internal static PostgresFrame Bind(string key, string value, bool binary = false, bool etag = false,
        string statement = "statement", string portal = "portal", short[]? parameterFormats = null,
        byte jsonbVersion = 1, int binaryEtagLength = 4)
    {
        short[] formats = parameterFormats ?? [binary ? (short)1 : (short)0];
        short Format(int index) => formats.Length == 0 ? (short)0 : formats[formats.Length == 1 ? 0 : index];
        byte[][] values = [Encoding.UTF8.GetBytes(key), Format(1) == 1 ? [jsonbVersion, .. Encoding.UTF8.GetBytes(value)] : Encoding.UTF8.GetBytes(value),
            Format(2) == 1 ? [0] : Encoding.UTF8.GetBytes("false")];
        if (etag)
        {
            byte[] bytes = Format(3) == 1 ? new byte[binaryEtagLength] : Encoding.UTF8.GetBytes("7");
            if (Format(3) == 1 && bytes.Length > 0) bytes[^1] = 7;
            values = [.. values, bytes];
        }
        return BindParameters(values, formats, statement, portal);
    }

    /// <summary>Creates an exact arbitrary parameter binding for supported reads and schema operations.</summary>
    internal static PostgresFrame BindParameters(byte[][] values, short[] formats, string statement = "statement", string portal = "portal")
    {
        using var stream = new MemoryStream();
        stream.Write(Encoding.UTF8.GetBytes(portal + "\0" + statement + "\0"));
        Int16((short)formats.Length);
        foreach (short format in formats) Int16(format);
        Int16((short)values.Length);
        foreach (byte[] bytes in values) { Int32(bytes.Length); stream.Write(bytes); }
        Int16(0);
        return new((byte)'B', stream.ToArray());
        void Int16(short value) { Span<byte> bytes = stackalloc byte[2]; BinaryPrimitives.WriteInt16BigEndian(bytes, value); stream.Write(bytes); }
        void Int32(int value) { Span<byte> bytes = stackalloc byte[4]; BinaryPrimitives.WriteInt32BigEndian(bytes, value); stream.Write(bytes); }
    }

    /// <summary>Creates a complete unlimited portal execution.</summary>
    internal static PostgresFrame Execute(string portal = "portal") => new((byte)'E', [.. Encoding.UTF8.GetBytes(portal), 0, 0, 0, 0, 0]);
}
