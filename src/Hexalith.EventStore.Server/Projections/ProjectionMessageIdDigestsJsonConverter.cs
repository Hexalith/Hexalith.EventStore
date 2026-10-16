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
            string? value = reader.GetString();
            Span<byte> target = bytes.AsSpan(index * SHA256.HashSizeInBytes, SHA256.HashSizeInBytes);
            if (value is null || !Convert.TryFromBase64String(value, target, out int written)
                || written != SHA256.HashSizeInBytes || Convert.ToBase64String(target) != value)
                (malformed ??= []).Add(index, value);
            index++;
        }
        return new ProjectionMessageIdDigests(bytes, malformed);
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
