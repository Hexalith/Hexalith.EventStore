using System.Text.Json.Serialization;

namespace Hexalith.EventStore.Contracts.Events;

/// <summary>
/// Carries one event's verified effective representation with its stored digest and signed route proof.
/// </summary>
public sealed class VerifiedEffectiveCommandEvent {
    private readonly byte[] _storedDigest;
    private readonly byte[] _effectivePayload;
    private readonly byte[] _routeClaim;
    private readonly byte[] _routeSignature;

    /// <summary>Initializes a command event with copied proof and payload bytes.</summary>
    [JsonConstructor]
    public VerifiedEffectiveCommandEvent(
        long sequence,
        byte[] storedDigest,
        string eventContractType,
        int payloadVersion,
        string serializationFormat,
        byte[] effectivePayload,
        byte[] routeClaim,
        string routeKeyId,
        byte[] routeSignature) {
        Sequence = sequence > 0
            ? sequence
            : throw new ArgumentOutOfRangeException(nameof(sequence), sequence, "Sequence must be positive.");
        ArgumentNullException.ThrowIfNull(storedDigest);
        ArgumentNullException.ThrowIfNull(routeSignature);
        ArgumentNullException.ThrowIfNull(effectivePayload);
        ArgumentNullException.ThrowIfNull(routeClaim);
        if (storedDigest.Length != 32) {
            throw new ArgumentException("StoredDigest must contain exactly 32 bytes.", nameof(storedDigest));
        }

        EventContractType = EventContractIdentityValidator.ValidateContractType(eventContractType, nameof(eventContractType));
        PayloadVersion = EventContractIdentityValidator.ValidatePayloadVersion(payloadVersion, nameof(payloadVersion));
        SerializationFormat = !string.IsNullOrWhiteSpace(serializationFormat)
            ? serializationFormat
            : throw new ArgumentException("SerializationFormat must not be empty.", nameof(serializationFormat));
        RouteKeyId = EventContractIdentityValidator.ValidateKeyId(routeKeyId, nameof(routeKeyId));
        if (routeSignature.Length != 64) {
            throw new ArgumentException("RouteSignature must contain exactly 64 bytes.", nameof(routeSignature));
        }

        // This transport carrier cannot authenticate whether the payload is zero-hop.
        // Admit the measured V1 ceiling here; verified readers enforce the 1 MiB hop limit.
        if (effectivePayload.Length > 64 * 1024 * 1024) {
            throw new ArgumentOutOfRangeException(nameof(effectivePayload), "ReadableLimit: an effective transport payload is limited to 64 MiB.");
        }

        if (routeClaim.Length > 1024 * 1024) {
            throw new ArgumentOutOfRangeException(nameof(routeClaim), "ProofLimit: a route claim is limited to 1 MiB.");
        }

        _storedDigest = storedDigest.ToArray();
        _effectivePayload = effectivePayload.ToArray();
        _routeClaim = routeClaim.ToArray();
        _routeSignature = routeSignature.ToArray();
    }

    /// <summary>Gets the event sequence number.</summary>
    [JsonPropertyName("sequence")]
    public long Sequence { get; }

    /// <summary>Gets a transport copy of the 32-byte stored digest.</summary>
    [JsonPropertyName("storedDigest")]
    public byte[] StoredDigest => _storedDigest.ToArray();

    /// <summary>Gets the canonical event contract type.</summary>
    [JsonPropertyName("eventContractType")]
    public string EventContractType { get; }

    /// <summary>Gets the effective payload schema version.</summary>
    [JsonPropertyName("payloadVersion")]
    public int PayloadVersion { get; }

    /// <summary>Gets the effective payload serialization format.</summary>
    [JsonPropertyName("serializationFormat")]
    public string SerializationFormat { get; }

    /// <summary>Gets a transport copy of the effective payload bytes.</summary>
    [JsonPropertyName("effectivePayload")]
    public byte[] EffectivePayload => _effectivePayload.ToArray();

    /// <summary>Gets a transport copy of the signed route claim.</summary>
    [JsonPropertyName("routeClaim")]
    public byte[] RouteClaim => _routeClaim.ToArray();

    /// <summary>Gets the route signing key identifier.</summary>
    [JsonPropertyName("routeKeyId")]
    public string RouteKeyId { get; }

    /// <summary>Gets a transport copy of the 64-byte route signature.</summary>
    [JsonPropertyName("routeSignature")]
    public byte[] RouteSignature => _routeSignature.ToArray();
}
