using System.Security.Cryptography;

namespace Hexalith.EventStore.Client.Events;

/// <summary>Owns one strictly decoded, immutable approved registry row.</summary>
internal sealed class EventRegistryRow : IDisposable
{
    private readonly byte[] _encoded;
    private readonly byte[][] _keys = [];
    private readonly int[] _fieldOffsets = [];
    private readonly string _fieldTypes = string.Empty;

    /// <summary>Admits exact D/V/A/E/F/S/G row framing before retaining its private bytes.</summary>
    internal EventRegistryRow(ReadOnlySpan<byte> encoded)
    {
        if (encoded.Length is < 1 or > 64 * 1024)
        {
            throw new ArgumentException("RegistryLimit: an encoded registry row is limited to 64 KiB.", nameof(encoded));
        }

        _encoded = encoded.ToArray();
        try
        {
            var reader = new EventEvolutionBinaryReader(_encoded);
            Tag = reader.ReadByte();
            (string keyTypes, string fieldTypes) = GetSchema(Tag);
            _fieldTypes = fieldTypes;
            _fieldOffsets = new int[fieldTypes.Length];
            _keys = new byte[keyTypes.Length][];
            for (int i = 0; i < keyTypes.Length; i++)
            {
                if (keyTypes[i] == 'O')
                {
                    if (reader.ReadByte() != 0)
                    {
                        throw new ArgumentException("The shared registry row requires the absent component key.", nameof(encoded));
                    }

                    _keys[i] = [0];
                }
                else if (keyTypes[i] == 'I')
                {
                    int offset = reader.Position;
                    int version = reader.ReadInt32();
                    ValidateVersion(version);
                    _keys[i] = _encoded.AsSpan(offset, 4).ToArray();
                }
                else
                {
                    ReadOnlySpan<byte> key = reader.ReadBytes(64 * 1024);
                    ValidateUtf8(key);
                    if (key.IsEmpty)
                    {
                        throw new ArgumentException("Registry primary-key strings cannot be empty.", nameof(encoded));
                    }

                    _keys[i] = key.ToArray();
                }
            }

            Domain = new System.Text.UTF8Encoding(false, true).GetString(_keys[0]);
            if (Tag is 0x44 or 0x56 or 0x45 or 0x46)
            {
                ValidateCanonicalType(_keys[1]);
            }

            if (Tag == 0x47 && !_keys[2].AsSpan().SequenceEqual("managed"u8) && !_keys[2].AsSpan().SequenceEqual("native"u8))
            {
                throw new ArgumentException("A dependency kind must be exactly managed or native.", nameof(encoded));
            }

            if (reader.ReadUInt16() != fieldTypes.Length)
            {
                throw new ArgumentException("A registry row has the wrong field count.", nameof(encoded));
            }

            for (int i = 0; i < fieldTypes.Length; i++)
            {
                if (reader.ReadByte() != i + 1)
                {
                    throw new ArgumentException("Registry field tags must be unique and consecutive in ascending order.", nameof(encoded));
                }

                _fieldOffsets[i] = reader.Position;

                switch (fieldTypes[i])
                {
                    case 'U':
                        ReadOnlySpan<byte> text = reader.ReadBytes(64 * 1024);
                        ValidateUtf8(text);
                        if (text.IsEmpty)
                        {
                            throw new ArgumentException("Registry implementation, format and policy identifiers cannot be empty.", nameof(encoded));
                        }

                        break;
                    case 'B':
                        _ = reader.ReadBytes(64 * 1024);
                        break;
                    case 'H':
                        _ = reader.ReadHash();
                        break;
                    case 'I':
                        int value = reader.ReadInt32();
                        if ((Tag == 0x44 && i == 1) || (Tag == 0x41 && i == 1)
                            || (Tag == 0x45 && i == 0) || (Tag == 0x46 && i < 2))
                        {
                            ValidateVersion(value);
                            if (Tag == 0x45 && value != System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(_keys[2]) + 1)
                            {
                                throw new ArgumentException("An upcast edge must advance exactly one version.", nameof(encoded));
                            }
                        }

                        break;
                }
            }

            reader.RequireEnd();
        }
        catch
        {
            CryptographicOperations.ZeroMemory(_encoded);
            foreach (byte[]? key in _keys)
            {
                if (key is not null)
                {
                    CryptographicOperations.ZeroMemory(key);
                }
            }

            throw;
        }
    }

    /// <summary>Gets the approved row-kind tag.</summary>
    internal byte Tag { get; }

    /// <summary>Gets the addressed domain.</summary>
    internal string Domain { get; }

    /// <summary>Gets immutable private row bytes for bounded hashing.</summary>
    internal ReadOnlySpan<byte> Encoded => _encoded;

    /// <summary>Reads one text primary-key component.</summary>
    internal string GetTextKey(int index)
        => new System.Text.UTF8Encoding(false, true).GetString(_keys[index]);

    /// <summary>Reads a version primary-key component.</summary>
    internal int GetVersionKey(int index)
        => System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(_keys[index]);

    /// <summary>Reads a one-based U field by its exact schema.</summary>
    internal string GetTextField(int tag)
    {
        var reader = GetFieldReader(tag, 'U');
        return reader.ReadString(64 * 1024);
    }

    /// <summary>Reads a one-based I field by its exact schema.</summary>
    internal int GetIntField(int tag)
    {
        var reader = GetFieldReader(tag, 'I');
        return reader.ReadInt32();
    }

    /// <summary>Returns an immutable private field slice for exact descriptor comparisons.</summary>
    internal ReadOnlySpan<byte> GetEncodedField(int tag)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(tag, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(tag, _fieldOffsets.Length);
        int start = _fieldOffsets[tag - 1];
        int end = tag == _fieldOffsets.Length ? _encoded.Length : _fieldOffsets[tag] - 1;
        return _encoded.AsSpan(start, end - start);
    }

    /// <summary>Compares separately framed keys by their unsigned scalar byte representations.</summary>
    internal int CompareKey(EventRegistryRow other)
    {
        int order = Tag.CompareTo(other.Tag);
        if (order != 0)
        {
            return order;
        }

        for (int i = 0; i < _keys.Length; i++)
        {
            order = _keys[i].AsSpan().SequenceCompareTo(other._keys[i]);
            if (order != 0)
            {
                return order;
            }
        }

        return 0;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        CryptographicOperations.ZeroMemory(_encoded);
        foreach (byte[] key in _keys)
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    private static (string Keys, string Fields) GetSchema(byte tag) => tag switch
    {
        0x44 => ("UU", "UIUHUHIBUHHH"),
        0x56 => ("UUI", "UHHUHHUUHH"),
        0x41 => ("UU", "UIUUHHUHHUHHUHH"),
        0x45 => ("UUI", "IUUUHHUHHUHH"),
        0x46 => ("UUU", "IIBBUUUHHUHHUHH"),
        0x53 => ("UO", "H" + string.Concat(Enumerable.Repeat("UHH", 10))),
        0x47 => ("UUU", "UHU"),
        _ => throw new ArgumentException("Unknown registry row tag."),
    };

    private static void ValidateUtf8(ReadOnlySpan<byte> value)
        => _ = new System.Text.UTF8Encoding(false, true).GetCharCount(value);

    private EventEvolutionBinaryReader GetFieldReader(int tag, char expectedType)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(tag, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(tag, _fieldOffsets.Length);
        if (_fieldTypes[tag - 1] != expectedType)
        {
            throw new ArgumentException("The requested field does not have that primitive type.", nameof(tag));
        }

        return new EventEvolutionBinaryReader(_encoded.AsSpan(_fieldOffsets[tag - 1]));
    }

    private static void ValidateVersion(int value)
    {
        if (value is < 1 or > 1024)
        {
            throw new ArgumentException("A registry payload version must be between 1 and 1024.");
        }
    }

    private static void ValidateCanonicalType(ReadOnlySpan<byte> value)
    {
        if (value.Length is < 1 or > 64)
        {
            throw new ArgumentException("A canonical registry event type must contain 1 to 64 ASCII bytes.");
        }

        for (int i = 0; i < value.Length; i++)
        {
            byte character = value[i];
            bool alphaNumeric = character is >= (byte)'a' and <= (byte)'z' or >= (byte)'0' and <= (byte)'9';
            if (!alphaNumeric && (character != '-' || i == 0 || i == value.Length - 1))
            {
                throw new ArgumentException("A canonical registry event type must use lower-case kebab-case.");
            }
        }
    }
}
