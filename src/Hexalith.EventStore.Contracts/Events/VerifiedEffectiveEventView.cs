using System.Text.Json.Serialization;

namespace Hexalith.EventStore.Contracts.Events;

/// <summary>
/// Carries a current event payload and its route proof across a transport boundary.
/// Construction and JSON binding do not verify the proof; receivers must authenticate it before use.
/// </summary>
public sealed class VerifiedEffectiveEventView {
    private readonly byte[] _storedDigest;
    private readonly byte[] _effectivePayload;
    private readonly byte[] _routeClaim;
    private readonly byte[] _routeSignature;

    /// <summary>Initializes the transport view while taking private copies of all byte arrays.</summary>
    [JsonConstructor]
    public VerifiedEffectiveEventView(
        long sequenceNumber,
        byte[] storedDigest,
        string eventContractType,
        int payloadVersion,
        string serializationFormat,
        byte[] effectivePayload,
        byte[] routeClaim,
        string routeKeyId,
        byte[] routeSignature) {
        SequenceNumber = sequenceNumber > 0
            ? sequenceNumber
            : throw new ArgumentOutOfRangeException(nameof(sequenceNumber), sequenceNumber, "SequenceNumber must be positive.");
        _storedDigest = storedDigest?.ToArray() ?? throw new ArgumentNullException(nameof(storedDigest));
        if (_storedDigest.Length != 32) {
            throw new ArgumentException("StoredDigest must contain exactly 32 bytes.", nameof(storedDigest));
        }

        EventContractType = EventContractIdentityValidator.ValidateContractType(eventContractType, nameof(eventContractType));
        PayloadVersion = EventContractIdentityValidator.ValidatePayloadVersion(payloadVersion, nameof(payloadVersion));
        SerializationFormat = !string.IsNullOrWhiteSpace(serializationFormat)
            ? serializationFormat
            : throw new ArgumentException("SerializationFormat must not be empty.", nameof(serializationFormat));
        _effectivePayload = effectivePayload?.ToArray() ?? throw new ArgumentNullException(nameof(effectivePayload));
        _routeClaim = routeClaim?.ToArray() ?? throw new ArgumentNullException(nameof(routeClaim));
        RouteKeyId = EventContractIdentityValidator.ValidateKeyId(routeKeyId, nameof(routeKeyId));
        _routeSignature = routeSignature?.ToArray() ?? throw new ArgumentNullException(nameof(routeSignature));
        if (_routeSignature.Length != 64) {
            throw new ArgumentException("RouteSignature must contain exactly 64 bytes.", nameof(routeSignature));
        }
    }

    /// <summary>Gets the event sequence number.</summary>
    [JsonPropertyName("sequenceNumber")]
    public long SequenceNumber { get; }

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
