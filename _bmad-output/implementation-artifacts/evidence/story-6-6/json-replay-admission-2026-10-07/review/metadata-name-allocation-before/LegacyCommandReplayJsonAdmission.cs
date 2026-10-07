using System.Buffers.Text;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Serialization;

namespace Hexalith.EventStore.Client.Handlers;

/// <summary>Measures legacy JSON replay before cloning, transport binding or base64 decoding.</summary>
/// <remarks>The supplied document already exists; this check cannot bound its original transport parsing.</remarks>
internal static class LegacyCommandReplayJsonAdmission
{
    private const long MaximumReadableBytes = 64L * 1024 * 1024;
    private const int MaximumMetadataBytes = 512 * 1024;

    /// <summary>Measures source bytes, document tokens, event count and readable payload without materialization.</summary>
    internal static (int Events, long ReadableBytes, long DocumentBytes) Measure(JsonElement source,
        CancellationToken cancellationToken, bool eventRoot = false)
    {
        ReadOnlySpan<byte> raw = JsonMarshal.GetRawUtf8Value(source);
        long documentBytes = checked(2L * raw.Length);
        var reader = new Utf8JsonReader(raw, new JsonReaderOptions
        {
            MaxDepth = 64, AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip,
        });
        try
        {
            while (reader.Read())
            {
                cancellationToken.ThrowIfCancellationRequested();
                // Conservatively cover source/private document token tables and growth slack.
                documentBytes = checked(documentBytes + 128);
                if (documentBytes > 256L * 1024 * 1024) { Refuse("LegacyArrayLimit"); }
            }
        }
        catch (JsonException)
        {
            Refuse("LegacyArrayLimit");
        }

        int events = 0;
        long readable = 0;
        if (eventRoot) { Event(source, ref events, ref readable, cancellationToken, contractEnvelope: false); }
        else { State(source, 0, ref events, ref readable, cancellationToken); }
        return (events, readable, documentBytes);
    }

    /// <summary>Checks the exact built-in envelope metadata image before transport binding decodes its payload.</summary>
    internal static void ValidateContractMetadata(JsonElement source, Action<EventEnvelope> validate, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (!IsWrapper(source)) { return; }
        _ = Property(source, "currentSequence");
        JsonElement events = Property(source, "events")!.Value;
        foreach (JsonElement item in events.EnumerateArray())
        {
            token.ThrowIfCancellationRequested();
            JsonElement? metadataJson = Property(item, "metadata");
            if (metadataJson is { ValueKind: JsonValueKind.Object } fields)
            {
                // Only fixed contract names have case-insensitive binding. Extension keys
                // remain application-owned, case-sensitive dictionary entries.
                foreach (string name in new[] { "messageId", "aggregateId", "aggregateType", "tenantId", "domain",
                    "sequenceNumber", "globalPosition", "timestamp", "correlationId", "causationId", "userId",
                    "domainServiceVersion", "eventTypeName", "metadataVersion", "serializationFormat",
                    "eventContractType", "payloadVersion" })
                {
                    token.ThrowIfCancellationRequested();
                    _ = Property(fields, name);
                }
            }

            EventMetadata metadata = metadataJson?.Deserialize<EventMetadata>(EventStorePayloadSerialization.Options)
                ?? throw new InvalidOperationException("ReplayScalarInvalid: replay envelope metadata is missing.");
            IReadOnlyDictionary<string, string>? extensions = Property(item, "extensions")
                ?.Deserialize<Dictionary<string, string>>(EventStorePayloadSerialization.Options);
            validate(new EventEnvelope(metadata, [], extensions));
        }

        if (Property(source, "snapshotState") is { } prior) { ValidateContractMetadata(prior, validate, token); }
    }

    private static void State(JsonElement state, int depth, ref int events, ref long readable, CancellationToken token)
    {
        if (state.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in state.EnumerateArray()) { Event(item, ref events, ref readable, token, contractEnvelope: false); }
        }
        else if (IsWrapper(state))
        {
            if (depth >= 64) { Refuse("LegacyArrayLimit"); }
            _ = Property(state, "currentSequence");
            JsonElement tail = Property(state, "events")!.Value;
            _ = Property(state, "lastSnapshotSequence");
            foreach (JsonElement item in tail.EnumerateArray()) { Event(item, ref events, ref readable, token, contractEnvelope: true); }
            if (Property(state, "snapshotState") is { } prior)
            {
                State(prior, depth + 1, ref events, ref readable, token);
            }
        }
    }

    private static bool IsWrapper(JsonElement value)
        => value.ValueKind == JsonValueKind.Object && value.TryGetProperty("currentSequence", out _)
            && value.TryGetProperty("events", out _);

    private static void Event(JsonElement item, ref int events, ref long readable, CancellationToken token, bool contractEnvelope)
    {
        token.ThrowIfCancellationRequested();
        if (++events > 100_000) { Refuse("LegacyArrayLimit"); }
        if (item.ValueKind != JsonValueKind.Object) { return; }
        ReadOnlySpan<byte> raw = JsonMarshal.GetRawUtf8Value(item);
        long payloadBytes = raw.Length;
        JsonElement? payload = contractEnvelope ? Property(item, "payload")
            : item.TryGetProperty("payload", out JsonElement inlinePayload) ? inlinePayload : null;
        long metadataBytes = 2;
        if (payload is { } body)
        {
            metadataBytes = EncodedMetadata(item, token, contractEnvelope: contractEnvelope);
            payloadBytes = body.ValueKind == JsonValueKind.String
                ? DecodedBase64Length(body, token)
                : JsonMarshal.GetRawUtf8Value(body).Length;
        }
        else
        {
            int members = 0;
            foreach (string name in new[] { "eventTypeName", "metadataVersion", "serializationFormat", "eventContractType", "payloadVersion" })
            {
                if (Property(item, name) is { } property)
                {
                    metadataBytes = checked(metadataBytes + (members++ == 0 ? 0 : 1) + name.Length + 3
                        + EncodedMetadata(property, token, eventRoot: false));
                    if (metadataBytes > MaximumMetadataBytes) { Refuse("MetadataLimit"); }
                }
            }
        }

        if (metadataBytes > MaximumMetadataBytes) { Refuse("MetadataLimit"); }
        if (payloadBytes > MaximumReadableBytes - readable) { Refuse("LegacyArrayLimit"); }
        readable += payloadBytes;
    }

    /// <summary>Rejects conflicting aliases using the same case-insensitive names as Web binding.</summary>
    private static JsonElement? Property(JsonElement json, string name)
    {
        JsonElement? found = null;
        foreach (JsonProperty property in json.EnumerateObject())
        {
            if (JsonMarshal.GetRawUtf8PropertyName(property).Length > MaximumMetadataBytes) { Refuse("MetadataLimit"); }
            if (!string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase)) { continue; }
            if (found is { } prior && !JsonElement.DeepEquals(prior, property.Value))
            {
                throw new InvalidOperationException("CapabilityMismatch: replay JSON contains conflicting property aliases.");
            }

            found = property.Value;
        }

        return found;
    }

    private static long EncodedMetadata(JsonElement value, CancellationToken token, bool eventRoot = true, bool contractEnvelope = false)
    {
        long bytes = 0;
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                bytes = 2;
                int members = 0;
                foreach (JsonProperty property in value.EnumerateObject())
                {
                    token.ThrowIfCancellationRequested();
                    bytes = checked(bytes + (members++ == 0 ? 0 : 1)
                        + EncodedString(JsonMarshal.GetRawUtf8PropertyName(property), token) + 1
                        + (eventRoot && string.Equals(property.Name, "payload", contractEnvelope ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)
                            ? 2 : EncodedMetadata(property.Value, token, eventRoot: false)));
                    if (bytes > MaximumMetadataBytes) { Refuse("MetadataLimit"); }
                }

                break;
            case JsonValueKind.Array:
                bytes = 2;
                int elements = 0;
                foreach (JsonElement item in value.EnumerateArray())
                {
                    bytes = checked(bytes + (elements++ == 0 ? 0 : 1) + EncodedMetadata(item, token, eventRoot: false));
                    if (bytes > MaximumMetadataBytes) { Refuse("MetadataLimit"); }
                }

                break;
            case JsonValueKind.String:
                ReadOnlySpan<byte> raw = JsonMarshal.GetRawUtf8Value(value);
                bytes = EncodedString(raw[1..^1], token);
                break;
            default:
                bytes = JsonMarshal.GetRawUtf8Value(value).Length;
                break;
        }

        if (bytes > MaximumMetadataBytes) { Refuse("MetadataLimit"); }
        return bytes;
    }

    private static long EncodedString(ReadOnlySpan<byte> value, CancellationToken token)
    {
        long bytes = 2;
        for (int offset = 0; offset < value.Length;)
        {
            token.ThrowIfCancellationRequested();
            Rune rune;
            if (value[offset] == '\\')
            {
                offset++;
                int scalar = value[offset++] switch
                {
                    (byte)'"' => '"', (byte)'\\' => '\\', (byte)'/' => '/',
                    (byte)'b' => 8, (byte)'f' => 12, (byte)'n' => 10, (byte)'r' => 13, (byte)'t' => 9,
                    (byte)'u' => ReadHex(value, ref offset),
                    _ => -1,
                };
                if (scalar is >= 0xd800 and <= 0xdbff)
                {
                    if (value.Length - offset < 6 || value[offset++] != '\\' || value[offset++] != 'u') { Refuse("ReplayScalarInvalid"); }
                    int low = ReadHex(value, ref offset);
                    if (low is < 0xdc00 or > 0xdfff) { Refuse("ReplayScalarInvalid"); }
                    scalar = 0x10000 + ((scalar - 0xd800) << 10) + low - 0xdc00;
                }

                if (!Rune.TryCreate(scalar, out rune)) { Refuse("ReplayScalarInvalid"); }
            }
            else
            {
                if (Rune.DecodeFromUtf8(value[offset..], out rune, out int consumed) != System.Buffers.OperationStatus.Done)
                {
                    Refuse("ReplayScalarInvalid");
                }

                offset += consumed;
            }

            bytes += rune.Value switch
            {
                8 or 9 or 10 or 12 or 13 or 92 => 2,
                _ => JavaScriptEncoder.Default.WillEncode(rune.Value) ? 6L * rune.Utf16SequenceLength : rune.Utf8SequenceLength,
            };
            if (bytes > MaximumMetadataBytes) { Refuse("MetadataLimit"); }
        }

        return bytes;
    }

    private static int ReadHex(ReadOnlySpan<byte> value, ref int offset)
        => Hex(value[offset++]) * 4096 + Hex(value[offset++]) * 256 + Hex(value[offset++]) * 16 + Hex(value[offset++]);

    /// <summary>Checks base64 syntax and measures the exact decode before payload allocation.</summary>
    internal static long DecodedBase64Length(JsonElement value, CancellationToken token)
        => ReadBase64(value, Span<byte>.Empty, decode: false, token);

    /// <summary>Decodes into an admitted buffer using only fixed quartet and three-byte scratch.</summary>
    internal static void DecodeBase64(JsonElement value, Span<byte> destination, CancellationToken token)
    {
        if (ReadBase64(value, destination, decode: true, token) != destination.Length)
        {
            throw new FormatException("Invalid replay payload base64 length.");
        }
    }

    private static int ReadBase64(JsonElement value, Span<byte> destination, bool decode, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (value.ValueKind != JsonValueKind.String) { throw new InvalidOperationException("Replay payload must be a base64 string."); }
        ReadOnlySpan<byte> raw = JsonMarshal.GetRawUtf8Value(value);
        ReadOnlySpan<byte> text = raw[1..^1];
        Span<byte> quartet = stackalloc byte[4];
        Span<byte> decoded = stackalloc byte[3];
        int count = 0;
        int length = 0;
        int untilCancellation = 0;
        bool ended = false;
        try
        {
            for (int offset = 0; offset < text.Length; offset++)
            {
                if (untilCancellation-- == 0) { token.ThrowIfCancellationRequested(); untilCancellation = 4095; }
                int character = text[offset];
                if (character == '\\')
                {
                    character = text[++offset] switch
                    {
                        (byte)'/' => '/',
                        (byte)'n' => '\n',
                        (byte)'r' => '\r',
                        (byte)'t' => '\t',
                        (byte)'u' => Hex(text[++offset]) * 4096 + Hex(text[++offset]) * 256
                            + Hex(text[++offset]) * 16 + Hex(text[++offset]),
                        _ => -1,
                    };
                }

                if (character is 9 or 10 or 13 or 32) { continue; }
                if (ended || character is < 0 or > 127) { throw new FormatException("Invalid replay payload base64."); }
                quartet[count++] = (byte)character;
                if (count != 4) { continue; }
                if (!Base64.IsValid(quartet, out int bytes)
                    || Base64.DecodeFromUtf8(quartet, decoded, out _, out int written) != System.Buffers.OperationStatus.Done
                    || written != bytes) { throw new FormatException("Invalid replay payload base64."); }
                if (decode) { decoded[..bytes].CopyTo(destination[length..]); }
                length = checked(length + bytes);
                ended = bytes != 3;
                count = 0;
            }

            token.ThrowIfCancellationRequested();
            if (count != 0) { throw new FormatException("Invalid replay payload base64."); }
            return length;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(decoded);
            CryptographicOperations.ZeroMemory(quartet);
        }
    }

    private static int Hex(byte value) => value switch
    {
        >= (byte)'0' and <= (byte)'9' => value - '0',
        >= (byte)'a' and <= (byte)'f' => value - 'a' + 10,
        >= (byte)'A' and <= (byte)'F' => value - 'A' + 10,
        _ => throw new FormatException("Invalid replay payload base64 escape."),
    };

    private static void Refuse(string reason)
        => throw new InvalidOperationException($"{reason}: legacy command-state JSON admission failed.");
}
