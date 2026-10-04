using System.Text;
using System.Text.Json;

namespace Hexalith.EventStore.Client.Events;

/// <summary>Encodes strict canonical decoded JSON values for approved options and evidence hashing.</summary>
/// <remarks>Callers must perform bounded raw admission before creating the supplied JSON tree.</remarks>
internal static class EventCanonicalJsonValueCodec
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private const int MaximumBytes = 64 * 1024 * 1024;

    /// <summary>Measures the bounded binary representation before allocation.</summary>
    internal static int MeasureValue(JsonElement value)
    {
        int nodes = 0;
        return checked((int)Measure(value, 1, ref nodes));
    }

    /// <summary>Renders the exact recursive binary value, including its root discriminator.</summary>
    internal static byte[] EncodeValue(JsonElement value)
    {
        int nodes = 0;
        int length = checked((int)Measure(value, 1, ref nodes));
        using var writer = new EventEvolutionBinaryWriter(length);
        WriteValue(value, writer);
        return writer.CopyEncodedBytes();
    }

    /// <summary>Renders sorted canonical UTF-8 text with exact escaping and one final LF.</summary>
    internal static byte[] EncodeText(JsonElement value, int maximumBytes = MaximumBytes)
    {
        int nodes = 0;
        long capacity = checked(MeasureText(value, 1, ref nodes) + 1);
        if (capacity > maximumBytes)
        {
            throw new ArgumentException("RegistryLimit: canonical JSON text and escaping exceed the admitted capacity.");
        }

        using var writer = new EventEvolutionBinaryWriter(checked((int)capacity));
        WriteText(value, writer);
        writer.WriteByte(10);
        return writer.CopyEncodedBytes();
    }

    /// <summary>Measures exact escaped text without allocating a rendering.</summary>
    private static long MeasureText(JsonElement value, int depth, ref int nodes)
    {
        if (depth > 64 || ++nodes > 1_000_000)
        {
            throw new ArgumentException("RegistryLimit: JSON depth or node count exceeds the admitted bound.");
        }

        long length;
        switch (value.ValueKind)
        {
            case JsonValueKind.String:
                length = MeasureString(value.GetString()!);
                break;
            case JsonValueKind.Number:
                length = EventCanonicalNumber.Measure(value.GetRawText(), MaximumBytes);
                break;
            case JsonValueKind.Array:
                length = 2;
                int elements = 0;
                foreach (JsonElement item in value.EnumerateArray())
                {
                    length = checked(length + (elements++ == 0 ? 0 : 1) + MeasureText(item, depth + 1, ref nodes));
                    CheckLimit(length);
                }

                break;
            case JsonValueKind.Object:
                length = 2;
                int fields = 0;
                foreach (JsonProperty property in GetSortedProperties(value))
                {
                    length = checked(length + (fields++ == 0 ? 0 : 1) + MeasureString(property.Name) + 1
                        + MeasureText(property.Value, depth + 1, ref nodes));
                    CheckLimit(length);
                }

                break;
            case JsonValueKind.Null:
            case JsonValueKind.True:
                length = 4;
                break;
            case JsonValueKind.False:
                length = 5;
                break;
            default:
                throw new ArgumentException("Undefined JSON values cannot be canonical evidence.");
        }

        CheckLimit(length);
        return length;
    }

    /// <summary>Measures literal Unicode and the approved minimal control escapes.</summary>
    private static long MeasureString(string value)
    {
        _ = StrictUtf8.GetByteCount(value);
        long length = 2;
        foreach (Rune rune in value.EnumerateRunes())
        {
            length = checked(length + (rune.Value is 34 or 92 or 8 or 9 or 10 or 12 or 13 ? 2
                : rune.Value < 32 ? 6 : rune.Utf8SequenceLength));
            CheckLimit(length);
        }

        return length;
    }

    private static long Measure(JsonElement value, int depth, ref int nodes)
    {
        if (depth > 64 || ++nodes > 1_000_000)
        {
            throw new ArgumentException("RegistryLimit: JSON depth or node count exceeds the admitted bound.");
        }

        long length = 1;
        switch (value.ValueKind)
        {
            case JsonValueKind.String:
                length = checked(5L + StrictUtf8.GetByteCount(value.GetString()!));
                break;
            case JsonValueKind.Number:
                length = checked(5L + EventCanonicalNumber.Measure(value.GetRawText(), MaximumBytes));
                break;
            case JsonValueKind.Array:
                length = 5;
                foreach (JsonElement item in value.EnumerateArray())
                {
                    length = checked(length + Measure(item, depth + 1, ref nodes));
                    CheckLimit(length);
                }

                break;
            case JsonValueKind.Object:
                length = 5;
                foreach (JsonProperty property in GetSortedProperties(value))
                {
                    length = checked(length + 5 + StrictUtf8.GetByteCount(property.Name) + Measure(property.Value, depth + 1, ref nodes));
                    CheckLimit(length);
                }

                break;
            case JsonValueKind.Null:
            case JsonValueKind.True:
            case JsonValueKind.False:
                break;
            default:
                throw new ArgumentException("Undefined JSON values cannot be canonical evidence.");
        }

        CheckLimit(length);
        return length;
    }

    private static void WriteValue(JsonElement value, EventEvolutionBinaryWriter writer)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Null: writer.WriteByte(0); break;
            case JsonValueKind.False: writer.WriteByte(1); break;
            case JsonValueKind.True: writer.WriteByte(2); break;
            case JsonValueKind.Number:
                writer.WriteByte(3); writer.WriteString(EventCanonicalNumber.Normalize(value.GetRawText(), MaximumBytes)); break;
            case JsonValueKind.String:
                writer.WriteByte(4); writer.WriteString(value.GetString()!); break;
            case JsonValueKind.Array:
                writer.WriteByte(5); writer.WriteUInt32(checked((uint)value.GetArrayLength()));
                foreach (JsonElement item in value.EnumerateArray()) { WriteValue(item, writer); }
                break;
            case JsonValueKind.Object:
                JsonProperty[] properties = GetSortedProperties(value);
                writer.WriteByte(6); writer.WriteUInt32(checked((uint)properties.Length));
                foreach (JsonProperty property in properties)
                {
                    writer.WriteByte(1); writer.WriteString(property.Name); WriteValue(property.Value, writer);
                }
                break;
            default: throw new ArgumentException("Undefined JSON values cannot be canonical evidence.");
        }
    }

    private static void WriteText(JsonElement value, EventEvolutionBinaryWriter writer)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Null: writer.WriteRaw("null"u8); break;
            case JsonValueKind.False: writer.WriteRaw("false"u8); break;
            case JsonValueKind.True: writer.WriteRaw("true"u8); break;
            case JsonValueKind.Number:
                foreach (char character in EventCanonicalNumber.Normalize(value.GetRawText(), MaximumBytes)) { writer.WriteByte((byte)character); }
                break;
            case JsonValueKind.String: WriteTextString(value.GetString()!, writer); break;
            case JsonValueKind.Array:
                writer.WriteByte((byte)'[');
                bool firstItem = true;
                foreach (JsonElement item in value.EnumerateArray())
                {
                    if (!firstItem) { writer.WriteByte((byte)','); }
                    firstItem = false; WriteText(item, writer);
                }
                writer.WriteByte((byte)']');
                break;
            case JsonValueKind.Object:
                writer.WriteByte((byte)'{');
                bool firstProperty = true;
                foreach (JsonProperty property in GetSortedProperties(value))
                {
                    if (!firstProperty) { writer.WriteByte((byte)','); }
                    firstProperty = false;
                    WriteTextString(property.Name, writer); writer.WriteByte((byte)':'); WriteText(property.Value, writer);
                }
                writer.WriteByte((byte)'}');
                break;
            default: throw new ArgumentException("Undefined JSON values cannot be canonical evidence.");
        }
    }

    private static void WriteTextString(string value, EventEvolutionBinaryWriter writer)
    {
        _ = StrictUtf8.GetByteCount(value);
        writer.WriteByte((byte)'"');
        Span<byte> encoded = stackalloc byte[4];
        foreach (Rune rune in value.EnumerateRunes())
        {
            switch (rune.Value)
            {
                case '"': writer.WriteRaw("\\\""u8); break;
                case '\\': writer.WriteRaw("\\\\"u8); break;
                case 8: writer.WriteRaw("\\b"u8); break;
                case 9: writer.WriteRaw("\\t"u8); break;
                case 10: writer.WriteRaw("\\n"u8); break;
                case 12: writer.WriteRaw("\\f"u8); break;
                case 13: writer.WriteRaw("\\r"u8); break;
                default:
                    if (rune.Value < 32)
                    {
                        writer.WriteRaw("\\u00"u8);
                        const string hex = "0123456789abcdef";
                        writer.WriteByte((byte)hex[rune.Value >> 4]); writer.WriteByte((byte)hex[rune.Value & 15]);
                    }
                    else
                    {
                        int count = rune.EncodeToUtf8(encoded);
                        writer.WriteRaw(encoded[..count]);
                    }
                    break;
            }
        }
        writer.WriteByte((byte)'"');
    }

    private static JsonProperty[] GetSortedProperties(JsonElement value)
    {
        int count = 0;
        long capacity = 0;
        foreach (JsonProperty property in value.EnumerateObject())
        {
            count++;
            capacity = checked(capacity + 256 + StrictUtf8.GetByteCount(property.Name) * 4L);
            CheckLimit(capacity);
        }

        JsonProperty[] properties = value.EnumerateObject().ToArray();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var ordering = new List<(JsonProperty Property, byte[] Name)>(count);
        try
        {
            foreach (JsonProperty property in properties)
            {
                _ = StrictUtf8.GetByteCount(property.Name);
                if (!names.Add(property.Name))
                {
                    throw new ArgumentException("Duplicate ordinal/case-insensitive JSON property names are forbidden.");
                }

                ordering.Add((property, StrictUtf8.GetBytes(property.Name)));
            }

            ordering.Sort(static (left, right) => left.Name.AsSpan().SequenceCompareTo(right.Name));
            for (int i = 0; i < properties.Length; i++)
            {
                properties[i] = ordering[i].Property;

            }

            return properties;
        }
        finally
        {
            foreach ((JsonProperty _, byte[] name) in ordering)
            {
                System.Security.Cryptography.CryptographicOperations.ZeroMemory(name);
            }
        }
    }

    private static void CheckLimit(long length)
    {
        if (length > MaximumBytes) { throw new ArgumentException("RegistryLimit: canonical JSON value exceeds 64 MiB."); }
    }
}
