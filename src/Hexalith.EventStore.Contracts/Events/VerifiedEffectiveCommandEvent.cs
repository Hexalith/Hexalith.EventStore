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
        Sequence = sequence;
        _storedDigest = storedDigest?.ToArray() ?? throw new ArgumentNullException(nameof(storedDigest));
        EventContractType = eventContractType ?? throw new ArgumentNullException(nameof(eventContractType));
        PayloadVersion = payloadVersion;
        SerializationFormat = serializationFormat ?? throw new ArgumentNullException(nameof(serializationFormat));
        _effectivePayload = effectivePayload?.ToArray() ?? throw new ArgumentNullException(nameof(effectivePayload));
        _routeClaim = routeClaim?.ToArray() ?? throw new ArgumentNullException(nameof(routeClaim));
        RouteKeyId = routeKeyId ?? throw new ArgumentNullException(nameof(routeKeyId));
        _routeSignature = routeSignature?.ToArray() ?? throw new ArgumentNullException(nameof(routeSignature));
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
