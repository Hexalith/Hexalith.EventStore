namespace Hexalith.EventStore.Contracts.Events;

/// <summary>Contains one addressed bounded raw-event page and its provider readback proof.</summary>
public sealed class AuthenticatedRawEventPage {
    private const int MaximumEvents = 256;
    private const int MaximumReadbackProofBytes = 1024 * 1024;
    private const long MaximumRawPageBytes = 128L * 1024 * 1024;
    private const long MaximumReadablePageBytes = 64L * 1024 * 1024;
    private readonly byte[] _readbackProof;
    private readonly IReadOnlyList<AuthenticatedRawEvent> _events;

    /// <summary>Initializes the addressed raw page, copying its proof and event collection.</summary>
    public AuthenticatedRawEventPage(
        string tenantId,
        string domain,
        string aggregateType,
        string aggregateId,
        long startSequence,
        long actorHead,
        string actorETag,
        string providerNamespace,
        IReadOnlyList<AuthenticatedRawEvent> events,
        byte[] readbackProof) {
        TenantId = RequireStrictUtf8(tenantId, nameof(tenantId));
        Domain = RequireStrictUtf8(domain, nameof(domain));
        AggregateType = RequireStrictUtf8(aggregateType, nameof(aggregateType));
        AggregateId = RequireStrictUtf8(aggregateId, nameof(aggregateId));
        if (startSequence < 1) {
            throw new ArgumentOutOfRangeException(nameof(startSequence));
        }

        if (actorHead < 0) {
            throw new ArgumentOutOfRangeException(nameof(actorHead));
        }

        StartSequence = startSequence;
        ActorHead = actorHead;
        ActorETag = RequireStrictUtf8(actorETag, nameof(actorETag));
        ProviderNamespace = RequireStrictUtf8(providerNamespace, nameof(providerNamespace));
        ArgumentNullException.ThrowIfNull(events);
        if (events.Count > MaximumEvents) {
            throw new ArgumentOutOfRangeException(nameof(events), "A raw event page is limited to 256 events.");
        }

        if (events.Count == 0 && startSequence <= actorHead) {
            throw new ArgumentException("An empty raw page cannot omit an event at or before the actor head.", nameof(events));
        }

        var lengths = new (int Raw, int? Encoding, int? Digest, int? Origin, int? Intent, int? Receipt)[events.Count];
        long sourceBytes = 0;
        for (int i = 0; i < events.Count; i++) {
            AuthenticatedRawEvent item = events[i] ?? throw new ArgumentException("A raw page cannot contain a null event.", nameof(events));
            _ = RequireStrictUtf8(item.StorageKey, nameof(events));
            ArgumentNullException.ThrowIfNull(item.RawEnvelope);
            if (item.SequenceNumber != checked(startSequence + i) || item.SequenceNumber > actorHead) {
                throw new ArgumentException("Raw page events must be contiguous and within the addressed actor head.", nameof(events));
            }

            int rawLength = GetLength(item.RawEnvelope, nameof(events));
            int? encodingLength = GetOptionalLength(item.EncodingEvidence, nameof(events));
            int? digestLength = GetOptionalLength(item.StoredDigestEvidence, nameof(events));
            int? originLength = GetOptionalLength(item.V1OriginEvidence, nameof(events));
            int? intentLength = GetOptionalLength(item.ActorIntentCertificate, nameof(events));
            int? receiptLength = GetOptionalLength(item.ActorCommitReceipt, nameof(events));
            lengths[i] = (rawLength, encodingLength, digestLength, originLength, intentLength, receiptLength);
            sourceBytes = checked(sourceBytes + rawLength + (encodingLength ?? 0) + (digestLength ?? 0)
                + (originLength ?? 0) + (intentLength ?? 0) + (receiptLength ?? 0));
            if (sourceBytes > MaximumRawPageBytes) {
                throw new ArgumentOutOfRangeException(nameof(events), "Raw event envelopes and sidecars are limited to 128 MiB per page.");
            }
        }

        ArgumentNullException.ThrowIfNull(readbackProof);
        if (readbackProof.Length is 0 or > MaximumReadbackProofBytes) {
            throw new ArgumentOutOfRangeException(nameof(readbackProof), "A provider readback proof must be between 1 byte and 1 MiB.");
        }

        var ownedEvents = new AuthenticatedRawEvent[events.Count];
        for (int i = 0; i < events.Count; i++) {
            AuthenticatedRawEvent item = events[i];
            (int rawLength, int? encodingLength, int? digestLength, int? originLength, int? intentLength, int? receiptLength) = lengths[i];
            ownedEvents[i] = item with {
                RawEnvelope = CopyPayload(item.RawEnvelope, rawLength),
                EncodingEvidence = CopyOptionalPayload(item.EncodingEvidence, encodingLength),
                StoredDigestEvidence = CopyOptionalPayload(item.StoredDigestEvidence, digestLength),
                V1OriginEvidence = CopyOptionalPayload(item.V1OriginEvidence, originLength),
                ActorIntentCertificate = CopyOptionalPayload(item.ActorIntentCertificate, intentLength),
                ActorCommitReceipt = CopyOptionalPayload(item.ActorCommitReceipt, receiptLength),
            };
        }

        _events = Array.AsReadOnly(ownedEvents);
        _readbackProof = readbackProof.ToArray();
    }

    /// <summary>Gets the addressed tenant.</summary>
    public string TenantId { get; }

    /// <summary>Gets the addressed domain.</summary>
    public string Domain { get; }

    /// <summary>Gets the addressed aggregate type.</summary>
    public string AggregateType { get; }

    /// <summary>Gets the addressed aggregate identifier.</summary>
    public string AggregateId { get; }

    /// <summary>Gets the first requested sequence number.</summary>
    public long StartSequence { get; }

    /// <summary>Gets the provider's observed actor head.</summary>
    public long ActorHead { get; }

    /// <summary>Gets the provider's observed actor ETag.</summary>
    public string ActorETag { get; }

    /// <summary>Gets the canonical provider namespace.</summary>
    public string ProviderNamespace { get; }

    /// <summary>Gets an immutable event collection.</summary>
    public IReadOnlyList<AuthenticatedRawEvent> Events => _events;

    /// <summary>Gets a transport copy of the provider readback proof.</summary>
    public byte[] ReadbackProof => _readbackProof.ToArray();

    /// <summary>Rejects a measured page whose readable payload total exceeds the approved 64 MiB ceiling.</summary>
    /// <param name="readablePayloadBytes">The checked sum measured after bounded parsing and unprotection.</param>
    public static void ValidateReadablePayloadBytes(long readablePayloadBytes) {
        if (readablePayloadBytes < 0 || readablePayloadBytes > MaximumReadablePageBytes) {
            throw new ArgumentOutOfRangeException(nameof(readablePayloadBytes), "Readable event payloads are limited to 64 MiB per page.");
        }
    }

    private static int GetLength(IReadOnlyPayload payload, string parameterName) {
        ArgumentNullException.ThrowIfNull(payload, parameterName);
        int length = payload.Length;
        if (length < 0) {
            throw new ArgumentException("An event evidence payload cannot report a negative length.", parameterName);
        }

        return length;
    }

    private static int? GetOptionalLength(IReadOnlyPayload? payload, string parameterName)
        => payload is null ? null : GetLength(payload, parameterName);

    private static IReadOnlyPayload? CopyOptionalPayload(IReadOnlyPayload? payload, int? length)
        => payload is null ? null : CopyPayload(payload, length ?? throw new InvalidOperationException("Payload length was not measured."));

    private static IReadOnlyPayload CopyPayload(IReadOnlyPayload source, int length) {
        var bytes = GC.AllocateUninitializedArray<byte>(length);
        source.CopyTo(0, bytes);
        if (source.Length != length) {
            throw new ArgumentException("An event evidence payload changed length while being copied.", nameof(source));
        }

        return new OwnedPayload(bytes);
    }

    private static string RequireStrictUtf8(string value, string parameterName) {
        ArgumentNullException.ThrowIfNull(value, parameterName);
        if (value.Length == 0) {
            throw new ArgumentException("The addressed value cannot be empty.", parameterName);
        }

        try {
            _ = new System.Text.UTF8Encoding(false, true).GetByteCount(value);
        }
        catch (System.Text.EncoderFallbackException exception) {
            throw new ArgumentException("The addressed value must contain valid Unicode scalar values.", parameterName, exception);
        }

        return value;
    }

    private sealed class OwnedPayload(byte[] bytes) : IReadOnlyPayload {
        public int Length => bytes.Length;

        public void CopyTo(int sourceOffset, Span<byte> destination) {
            if (sourceOffset < 0 || sourceOffset > bytes.Length || destination.Length > bytes.Length - sourceOffset) {
                throw new ArgumentOutOfRangeException(nameof(sourceOffset));
            }

            bytes.AsSpan(sourceOffset, destination.Length).CopyTo(destination);
        }
    }
}
