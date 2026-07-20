using System.Buffers;
using System.Buffers.Binary;
using System.Text;
using System.Text.Json;

namespace Hexalith.EventStore.Server.Commands;

/// <summary>Encodes trusted semantic intent with deterministic type tags and length prefixes.</summary>
public sealed class CanonicalIdempotencyIntentEncoder
{
    private const int MaxCanonicalPayloadBytes = 65_536;
    private const int MaxSemanticOptions = 128;
    private readonly JsonDocumentOptions _documentOptions = new()
    {
        AllowTrailingCommas = false,
        CommentHandling = JsonCommentHandling.Disallow,
    };

    /// <summary>Encodes one trusted adapter result into canonical bytes.</summary>
    /// <param name="adapter">The registered adapter supplying fixed authority metadata.</param>
    /// <param name="intent">The schema-normalized semantic intent.</param>
    /// <returns>Deterministic canonical bytes.</returns>
    public byte[] Encode(IIdempotencyIntentAdapter adapter, IdempotencyCanonicalIntent intent)
    {
        ArgumentNullException.ThrowIfNull(adapter);
        ArgumentNullException.ThrowIfNull(intent);

        byte[] canonicalPayload = CanonicalizeJson(intent.SemanticPayload);
        try
        {
            var output = new ArrayBufferWriter<byte>();
            WriteField(output, 1, "hexalith-eventstore-idempotency-intent-v1"u8);
            WriteField(output, 2, Encoding.UTF8.GetBytes(adapter.AdapterId));
            WriteField(output, 3, Encoding.UTF8.GetBytes(adapter.OperationId));
            WriteIntegerField(output, 4, adapter.DescriptorVersion);
            WriteIntegerField(output, 5, (int)adapter.RetentionTier);
            WriteField(output, 6, Encoding.UTF8.GetBytes(intent.CanonicalTarget));
            WriteField(output, 7, canonicalPayload);
            WriteField(output, 8, EncodeOptions(intent.SemanticOptions));
            WriteField(output, 9, Encoding.UTF8.GetBytes(intent.PolicyVersion));
            WriteField(output, 10, Encoding.UTF8.GetBytes(intent.DelegatedTaskScope ?? string.Empty));
            WriteField(output, 11, Encoding.UTF8.GetBytes(intent.CredentialScope ?? string.Empty));
            return output.WrittenSpan.ToArray();
        }
        finally
        {
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(canonicalPayload);
        }
    }

    private byte[] CanonicalizeJson(byte[] payload)
    {
        if (payload.Length > MaxCanonicalPayloadBytes)
        {
            throw new InvalidOperationException("Trusted canonical intent payload exceeds the supported size.");
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(payload, _documentOptions);
            var output = new ArrayBufferWriter<byte>();
            using (var writer = new Utf8JsonWriter(output, new JsonWriterOptions { Indented = false }))
            {
                WriteCanonicalElement(writer, document.RootElement);
            }

            return output.WrittenSpan.ToArray();
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException("Trusted canonical intent payload is not valid JSON.", exception);
        }
    }

    private static void WriteCanonicalElement(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                var properties = new List<JsonProperty>();
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (JsonProperty property in element.EnumerateObject())
                {
                    if (!names.Add(property.Name))
                    {
                        throw new InvalidOperationException(
                            "Trusted canonical intent contains a duplicate JSON property.");
                    }

                    properties.Add(property);
                }

                foreach (JsonProperty property in properties.OrderBy(static property => property.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonicalElement(writer, property.Value);
                }

                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (JsonElement item in element.EnumerateArray())
                {
                    WriteCanonicalElement(writer, item);
                }

                writer.WriteEndArray();
                break;
            case JsonValueKind.String:
                writer.WriteStringValue(element.GetString());
                break;
            case JsonValueKind.Number:
                writer.WriteRawValue(element.GetRawText(), skipInputValidation: false);
                break;
            case JsonValueKind.True:
                writer.WriteBooleanValue(true);
                break;
            case JsonValueKind.False:
                writer.WriteBooleanValue(false);
                break;
            case JsonValueKind.Null:
                writer.WriteNullValue();
                break;
            default:
                throw new InvalidOperationException("Trusted canonical intent contains an unsupported JSON value.");
        }
    }

    private static byte[] EncodeOptions(IReadOnlyDictionary<string, string>? options)
    {
        if (options is null || options.Count == 0)
        {
            return [];
        }

        if (options.Count > MaxSemanticOptions)
        {
            throw new InvalidOperationException("Trusted canonical intent contains too many semantic options.");
        }

        var output = new ArrayBufferWriter<byte>();
        WriteInteger(output, options.Count);
        foreach (KeyValuePair<string, string> option in options.OrderBy(static option => option.Key, StringComparer.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(option.Key) || option.Value is null)
            {
                throw new InvalidOperationException("Trusted canonical intent contains an invalid semantic option.");
            }

            WriteLengthPrefixed(output, Encoding.UTF8.GetBytes(option.Key));
            WriteLengthPrefixed(output, Encoding.UTF8.GetBytes(option.Value));
        }

        return output.WrittenSpan.ToArray();
    }

    private static void WriteIntegerField(ArrayBufferWriter<byte> output, byte tag, int value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(bytes, value);
        WriteField(output, tag, bytes);
    }

    private static void WriteField(ArrayBufferWriter<byte> output, byte tag, ReadOnlySpan<byte> value)
    {
        Span<byte> target = output.GetSpan(1 + sizeof(int) + value.Length);
        target[0] = tag;
        BinaryPrimitives.WriteInt32BigEndian(target[1..], value.Length);
        value.CopyTo(target[(1 + sizeof(int))..]);
        output.Advance(1 + sizeof(int) + value.Length);
    }

    private static void WriteLengthPrefixed(ArrayBufferWriter<byte> output, ReadOnlySpan<byte> value)
    {
        Span<byte> target = output.GetSpan(sizeof(int) + value.Length);
        BinaryPrimitives.WriteInt32BigEndian(target, value.Length);
        value.CopyTo(target[sizeof(int)..]);
        output.Advance(sizeof(int) + value.Length);
    }

    private static void WriteInteger(ArrayBufferWriter<byte> output, int value)
    {
        Span<byte> target = output.GetSpan(sizeof(int));
        BinaryPrimitives.WriteInt32BigEndian(target, value);
        output.Advance(sizeof(int));
    }
}
