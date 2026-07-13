using System.Buffers;
using System.Text.Json;

namespace Hexalith.EventStore.Client.Projections;

/// <summary>
/// Deterministic, culture-invariant JSON canonicalization used for fingerprinting and for the bytes
/// persisted by coordinated batches. Object properties are emitted in ordinal name order so the output is
/// independent of reflection/serializer property ordering.
/// </summary>
/// <remarks>
/// The serializer options are intentionally fixed to <see cref="JsonSerializerDefaults.Web"/> (the DAPR
/// default) rather than a consumer-supplied <c>DaprClient.JsonSerializerOptions</c>: the canonical bytes are
/// the versioned fingerprint material, so they must be stable across deployments and independent of runtime
/// configuration (changing them is a versioned contract change validated by a golden-vector test).
/// Coordinated batch writes therefore require the store's effective DAPR JSON options to remain the Web
/// defaults; a consumer that customizes <c>DaprClient.JsonSerializerOptions</c> (custom converters, naming
/// policy) must not use the batch seam, because the single-key read path (<c>DaprReadModelStore.GetAsync</c>)
/// deserializes with those custom options and batch-written values would not round-trip. A registration
/// guard test pins the default assumption.
/// </remarks>
internal static class ReadModelBatchCanonicalJson {
    private static readonly JsonSerializerOptions s_serializerOptions = new(JsonSerializerDefaults.Web);
    private static readonly JsonWriterOptions s_writerOptions = new() { Indented = false, SkipValidation = true };

    /// <summary>Serializes a value with DAPR-compatible options and canonicalizes the result.</summary>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="value">The value to serialize.</param>
    /// <returns>Canonical UTF-8 JSON bytes.</returns>
    public static ReadOnlyMemory<byte> Serialize<TValue>(TValue value) {
        byte[] raw = JsonSerializer.SerializeToUtf8Bytes(value, s_serializerOptions);
        return Canonicalize(raw);
    }

    /// <summary>Re-emits UTF-8 JSON with ordinally sorted object keys and no insignificant whitespace.</summary>
    /// <param name="utf8Json">The UTF-8 JSON bytes to canonicalize.</param>
    /// <returns>Canonical UTF-8 JSON bytes.</returns>
    public static byte[] Canonicalize(ReadOnlyMemory<byte> utf8Json) {
        using var document = JsonDocument.Parse(utf8Json);
        var buffer = new ArrayBufferWriter<byte>(utf8Json.Length);
        using (var writer = new Utf8JsonWriter(buffer, s_writerOptions)) {
            WriteCanonical(document.RootElement, writer);
        }

        return buffer.WrittenSpan.ToArray();
    }

    private static void WriteCanonical(JsonElement element, Utf8JsonWriter writer) {
        switch (element.ValueKind) {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (JsonProperty property in element.EnumerateObject()
                    .OrderBy(static p => p.Name, StringComparer.Ordinal)) {
                    writer.WritePropertyName(property.Name);
                    WriteCanonical(property.Value, writer);
                }

                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (JsonElement item in element.EnumerateArray()) {
                    WriteCanonical(item, writer);
                }

                writer.WriteEndArray();
                break;
            default:
                element.WriteTo(writer);
                break;
        }
    }
}
