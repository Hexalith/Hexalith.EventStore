using System.Buffers;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Commands;

namespace Hexalith.EventStore.Server.Commands;

/// <summary>Encodes trusted semantic intent with deterministic type tags and length prefixes.</summary>
public sealed class CanonicalIdempotencyIntentEncoder
{
    private const int MaxCanonicalIntentBytes = 131_072;
    private const int MaxCanonicalPayloadBytes = 65_536;
    private const int MaxMetadataFieldBytes = 4_096;
    private const int MaxSemanticOptions = 128;
    private const int MaxSemanticOptionsBytes = 65_536;
    private readonly JsonDocumentOptions _documentOptions = new()
    {
        AllowTrailingCommas = false,
        CommentHandling = JsonCommentHandling.Disallow,
    };

    /// <summary>Encodes one trusted adapter result into canonical bytes.</summary>
    /// <param name="adapterId">The snapshotted server-owned adapter identifier.</param>
    /// <param name="operationId">The snapshotted server-owned operation identifier.</param>
    /// <param name="descriptorVersion">The snapshotted canonical descriptor version.</param>
    /// <param name="retentionTier">The snapshotted replay retention tier.</param>
    /// <param name="intent">The schema-normalized semantic intent.</param>
    /// <returns>Deterministic canonical bytes.</returns>
    public byte[] Encode(
        string adapterId,
        string operationId,
        int descriptorVersion,
        IdempotencyReplayRetentionTier retentionTier,
        IdempotencyCanonicalIntent intent)
    {
        ArgumentNullException.ThrowIfNull(adapterId);
        ArgumentNullException.ThrowIfNull(operationId);
        ArgumentNullException.ThrowIfNull(intent);

        byte[] canonicalPayload = CanonicalizeJson(intent.SemanticPayload);
        byte[]? encodedOptions = null;
        try
        {
            var output = new ArrayBufferWriter<byte>();
            try
            {
                WriteField(output, 1, "hexalith-eventstore-idempotency-intent-v1"u8);
                WriteStringField(output, 2, adapterId, "adapter identifier");
                WriteStringField(output, 3, operationId, "operation identifier");
                WriteIntegerField(output, 4, descriptorVersion);
                WriteIntegerField(output, 5, (int)retentionTier);
                WriteStringField(output, 6, intent.CanonicalTarget, "canonical target");
                WriteField(output, 7, canonicalPayload);
                encodedOptions = EncodeOptions(intent.SemanticOptions);
                WriteField(output, 8, encodedOptions);
                WriteStringField(output, 9, intent.PolicyVersion, "policy version");
                WriteStringField(
                    output,
                    10,
                    intent.DelegatedTaskScope ?? string.Empty,
                    "delegated task scope");
                WriteStringField(
                    output,
                    11,
                    intent.CredentialScope ?? string.Empty,
                    "credential scope");
                if (output.WrittenCount > MaxCanonicalIntentBytes)
                {
                    throw new InvalidOperationException("Trusted canonical intent exceeds the supported size.");
                }

                return output.WrittenSpan.ToArray();
            }
            finally
            {
                output.Clear();
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(canonicalPayload);
            if (encodedOptions is not null)
            {
                CryptographicOperations.ZeroMemory(encodedOptions);
            }
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

            if (output.WrittenCount > MaxCanonicalPayloadBytes)
            {
                output.Clear();
                throw new InvalidOperationException(
                    "Trusted canonical intent payload exceeds the supported size after canonicalization.");
            }

            byte[] result = output.WrittenSpan.ToArray();
            output.Clear();
            return result;
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
        try
        {
            WriteInteger(output, options.Count);
            foreach (KeyValuePair<string, string> option in options.OrderBy(static option => option.Key, StringComparer.Ordinal))
            {
                if (string.IsNullOrWhiteSpace(option.Key) || option.Value is null)
                {
                    throw new InvalidOperationException("Trusted canonical intent contains an invalid semantic option.");
                }

                int keyLength = GetBoundedUtf8Length(option.Key, "semantic option key");
                int valueLength = GetBoundedUtf8Length(option.Value, "semantic option value");
                if (output.WrittenCount + (2 * sizeof(int)) + keyLength + valueLength > MaxSemanticOptionsBytes)
                {
                    throw new InvalidOperationException(
                        "Trusted canonical intent semantic options exceed the supported size.");
                }

                byte[] keyBytes = Encoding.UTF8.GetBytes(option.Key);
                byte[] valueBytes = Encoding.UTF8.GetBytes(option.Value);
                try
                {
                    WriteLengthPrefixed(output, keyBytes);
                    WriteLengthPrefixed(output, valueBytes);
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(keyBytes);
                    CryptographicOperations.ZeroMemory(valueBytes);
                }
            }

            return output.WrittenSpan.ToArray();
        }
        finally
        {
            output.Clear();
        }
    }

    private static int GetBoundedUtf8Length(string value, string fieldName)
    {
        int length = Encoding.UTF8.GetByteCount(value);
        if (length > MaxMetadataFieldBytes)
        {
            throw new InvalidOperationException(
                $"Trusted canonical intent {fieldName} exceeds the supported size.");
        }

        return length;
    }

    private static void WriteIntegerField(ArrayBufferWriter<byte> output, byte tag, int value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(bytes, value);
        WriteField(output, tag, bytes);
    }

    private static void WriteStringField(
        ArrayBufferWriter<byte> output,
        byte tag,
        string value,
        string fieldName)
    {
        _ = GetBoundedUtf8Length(value, fieldName);
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        try
        {
            WriteField(output, tag, bytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
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
