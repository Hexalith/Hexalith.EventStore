using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>Preserves the historical JSON list exactly while avoiding a retained string per complete identity digest.</summary>
internal sealed class ProjectionMessageIdDigestsJsonConverter : JsonConverter<IReadOnlyList<string>>
{
    /// <inheritdoc/>
    public override IReadOnlyList<string>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray) throw new JsonException();
        Utf8JsonReader scan = reader;
        int count = 0;
        while (scan.Read() && scan.TokenType != JsonTokenType.EndArray)
        {
            if (scan.TokenType is not JsonTokenType.String and not JsonTokenType.Null) throw new JsonException();
            count = checked(count + 1);
        }
        if (scan.TokenType != JsonTokenType.EndArray) throw new JsonException();
        var bytes = new byte[checked(count * SHA256.HashSizeInBytes)];
        Dictionary<int, string?>? malformed = null;
        int index = 0;
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            Span<byte> target = bytes.AsSpan(index * SHA256.HashSizeInBytes, SHA256.HashSizeInBytes);
            if (!TryReadCanonicalDigest(ref reader, target))
                (malformed ??= []).Add(index, reader.GetString());
            index++;
        }
        return new ProjectionMessageIdDigests(bytes, malformed);
    }

    /// <summary>Validates the exact decoded legacy Base64 representation without allocating a digest string.</summary>
    internal static bool TryReadCanonicalDigest(ref Utf8JsonReader reader, scoped Span<byte> target)
    {
        if (reader.TokenType != JsonTokenType.String
            || (reader.HasValueSequence ? reader.ValueSequence.Length : reader.ValueSpan.Length) > 264) return false;
        Span<char> encoded = stackalloc char[264];
        Span<char> canonical = stackalloc char[44];
        int length = reader.CopyString(encoded);
        return length == 44 && Convert.TryFromBase64Chars(encoded[..length], target, out int written) && written == SHA256.HashSizeInBytes
            && Convert.TryToBase64Chars(target, canonical, out int characters) && characters == length
            && encoded[..length].SequenceEqual(canonical);
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, IReadOnlyList<string> value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        int count = 0;
        foreach (string digest in value)
        {
            writer.WriteStringValue(digest);
            if (++count % 128 == 0) writer.Flush();
        }
        writer.WriteEndArray();
    }
}
