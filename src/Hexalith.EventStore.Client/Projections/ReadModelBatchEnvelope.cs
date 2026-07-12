using System.Text;
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

    private static readonly byte[] s_discriminatorToken = Encoding.UTF8.GetBytes("\"" + DiscriminatorPropertyName + "\"");
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

    /// <summary>Determines whether raw stored bytes are a batch envelope (cheap discriminator scan).</summary>
    /// <param name="raw">The raw stored bytes.</param>
    /// <returns><see langword="true"/> when the bytes contain the envelope discriminator.</returns>
    public static bool IsEnvelope(ReadOnlySpan<byte> raw) => raw.IndexOf(s_discriminatorToken) >= 0;

    /// <summary>Serializes this envelope to canonical UTF-8 bytes.</summary>
    /// <returns>The serialized envelope bytes.</returns>
    public ReadOnlyMemory<byte> ToBytes() =>
        ReadModelBatchCanonicalJson.Canonicalize(JsonSerializer.SerializeToUtf8Bytes(this, s_json));

    /// <summary>Deserializes an envelope from raw bytes.</summary>
    /// <param name="raw">The raw stored bytes.</param>
    /// <returns>The envelope, or <see langword="null"/> when the bytes are not a valid envelope.</returns>
    public static ReadModelBatchEnvelope? FromBytes(ReadOnlyMemory<byte> raw) {
        try {
            return JsonSerializer.Deserialize<ReadModelBatchEnvelope>(raw.Span, s_json);
        }
        catch (JsonException) {
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
