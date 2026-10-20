using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hexalith.EventStore.Client.Projections;

/// <summary>
/// A platform-owned pending envelope stored at a logical key during the resumable protocol. It carries the
/// previous committed value (returned by reads while the marker is prepared/aborting) and the candidate
/// value (returned only after the marker commits). Envelopes are compacted to normal committed values or
/// deletions after commit; mixed compacted and committed-envelope state reads identically.
/// </summary>
internal sealed class ReadModelBatchEnvelope {
    /// <summary>The discriminator property name used to detect an envelope in raw stored bytes.</summary>
    public const string DiscriminatorPropertyName = "$hxrmb";

    private static readonly JsonSerializerOptions s_json = new(JsonSerializerDefaults.Web);

    /// <summary>Gets or sets the envelope format version (also the detection discriminator).</summary>
    [JsonPropertyName(DiscriminatorPropertyName)]
    public int EnvelopeVersion { get; set; } = 1;

    /// <summary>Gets or sets the opaque scope hash of the owning batch.</summary>
    [JsonPropertyName("scope")]
    public string ScopeHash { get; set; } = string.Empty;

    /// <summary>Gets or sets the fingerprint of the owning batch.</summary>
    [JsonPropertyName("fp")]
    public string Fingerprint { get; set; } = string.Empty;

    /// <summary>Gets or sets the operation ordinal.</summary>
    [JsonPropertyName("ord")]
    public int Ordinal { get; set; }

    /// <summary>Gets or sets a value indicating whether the operation is a delete.</summary>
    [JsonPropertyName("del")]
    public bool IsDelete { get; set; }

    /// <summary>Gets or sets the base64 of the previous committed raw value (null when previously absent).</summary>
    [JsonPropertyName("prev")]
    public string? PreviousBase64 { get; set; }

    /// <summary>Gets or sets the base64 of the candidate canonical value (null for a delete).</summary>
    [JsonPropertyName("cand")]
    public string? CandidateBase64 { get; set; }

    /// <summary>Gets or sets the original inclusive source deadline for a v2 candidate.</summary>
    [JsonPropertyName("expires")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTimeOffset? ExpiresAtUtc { get; set; }

    /// <summary>Detects an actual top-level batch discriminator; ordinary strings and nested metadata are preserved.</summary>
    /// <param name="raw">The raw stored bytes.</param>
    /// <returns><see langword="true"/> when the top-level discriminator exists or malformed envelope-like data requires refusal.</returns>
    public static bool IsEnvelope(ReadOnlySpan<byte> raw)
    {
        var reader = new Utf8JsonReader(raw);
        try
        {
            while (reader.Read())
                if (reader.TokenType == JsonTokenType.PropertyName && reader.CurrentDepth == 1
                    && reader.ValueTextEquals(DiscriminatorPropertyName)) return true;
        }
        catch (JsonException) { return true; } // malformed envelope-like bytes remain fail-closed
        return false;
    }

    /// <summary>Serializes this envelope to canonical UTF-8 bytes.</summary>
    /// <returns>The serialized envelope bytes.</returns>
    public ReadOnlyMemory<byte> ToBytes() =>
        ReadModelBatchCanonicalJson.Canonicalize(JsonSerializer.SerializeToUtf8Bytes(this, s_json));

    /// <summary>Deserializes an envelope from raw bytes.</summary>
    /// <param name="raw">The raw stored bytes.</param>
    /// <returns>The envelope, or <see langword="null"/> when the bytes are not a valid envelope.</returns>
    /// <remarks>
    /// Decoding rejects missing/duplicate fields and malformed candidate or previous bytes.
    /// Normal data containing the discriminator only as a value or nested property remains ordinary data.
    /// </remarks>
    public static ReadModelBatchEnvelope? FromBytes(ReadOnlyMemory<byte> raw) {
        try {
            using JsonDocument document = JsonDocument.Parse(raw);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty(DiscriminatorPropertyName, out _)) {
                return null;
            }

            int version = document.RootElement.GetProperty(DiscriminatorPropertyName).GetInt32();
            string[] required = version == 2
                ? [DiscriminatorPropertyName, "scope", "fp", "ord", "del", "prev", "cand", "expires"]
                : [DiscriminatorPropertyName, "scope", "fp", "ord", "del", "prev", "cand"];
            JsonProperty[] properties = document.RootElement.EnumerateObject().ToArray();
            if (properties.Length != required.Length || required.Any(name => properties.Count(property => property.Name == name) != 1)) return null;
            ReadModelBatchEnvelope? envelope = JsonSerializer.Deserialize<ReadModelBatchEnvelope>(raw.Span, s_json);
            if (envelope is null || envelope.EnvelopeVersion is not (1 or 2)
                || (envelope.EnvelopeVersion == 2) != (envelope.ExpiresAtUtc is not null)
                || envelope.IsDelete && envelope.ExpiresAtUtc is not null || envelope.Ordinal < 0
                || envelope.IsDelete != (envelope.CandidateBase64 is null)) return null;
            if (envelope.PreviousBase64 is not null)
            {
                using JsonDocument previous = JsonDocument.Parse(envelope.PreviousBytes());
                if (IsEnvelope(envelope.PreviousBytes().Span)) return null;
            }
            if (!envelope.IsDelete)
            {
                using JsonDocument candidate = JsonDocument.Parse(envelope.CandidateBytes());
                if (IsEnvelope(envelope.CandidateBytes().Span)) return null;
            }
            return envelope;
        }
        catch (Exception exception) when (exception is JsonException or FormatException) {
            return null;
        }
    }

    /// <summary>Gets the previous raw value bytes, or empty when previously absent.</summary>
    /// <returns>The previous raw bytes.</returns>
    public ReadOnlyMemory<byte> PreviousBytes() =>
        PreviousBase64 is null ? ReadOnlyMemory<byte>.Empty : Convert.FromBase64String(PreviousBase64);

    /// <summary>Gets the candidate raw value bytes, or empty for a delete.</summary>
    /// <returns>The candidate raw bytes.</returns>
    public ReadOnlyMemory<byte> CandidateBytes() =>
        CandidateBase64 is null ? ReadOnlyMemory<byte>.Empty : Convert.FromBase64String(CandidateBase64);
}
