using System.Collections.Frozen;
using System.Security.Cryptography;
using System.Text;

using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Results;

namespace Hexalith.EventStore.DomainService;

/// <summary>Preflights declared V1 serializer bounds before once-only capped serialization.</summary>
/// <remarks>This local producer neither authenticates source state nor advertises negotiated or evidence-required writer readiness.</remarks>
internal sealed class BoundedV1DomainResultProducer
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private const long MaximumResultBytes = 128L * 1024 * 1024;
    private const long MaximumScratchBytes = 128L * 1024 * 1024;
    private readonly FrozenDictionary<Type, BoundedV1EventSerialization> _serializers;
    private readonly int _readableLimit;

    /// <summary>Admits exact serializer declarations, retaining 1 MiB unless a larger measured ceiling is explicitly supplied.</summary>
    internal BoundedV1DomainResultProducer(IReadOnlyList<BoundedV1EventSerialization> serializers,
        int measuredReadableLimit = 1024 * 1024)
    {
        ArgumentNullException.ThrowIfNull(serializers);
        if (measuredReadableLimit is < 1 or > 64 * 1024 * 1024 || serializers.Count > 65_536)
        {
            throw new ArgumentException("PayloadLimit: invalid measured V1 serializer profile.");
        }

        _readableLimit = measuredReadableLimit;
        var admitted = new Dictionary<Type, BoundedV1EventSerialization>();
        var aliases = new HashSet<string>(StringComparer.Ordinal);
        foreach (BoundedV1EventSerialization serializer in serializers)
        {
            ArgumentNullException.ThrowIfNull(serializer);
            ArgumentNullException.ThrowIfNull(serializer.PayloadType);
            ArgumentNullException.ThrowIfNull(serializer.SerializeAsync);
            ArgumentException.ThrowIfNullOrEmpty(serializer.ExactAlias);
            ArgumentException.ThrowIfNullOrEmpty(serializer.Format);
            if (serializer.MaximumPayloadBytes is < 1 || serializer.MaximumPayloadBytes > measuredReadableLimit
                || !typeof(IEventPayload).IsAssignableFrom(serializer.PayloadType)
                || !admitted.TryAdd(serializer.PayloadType, serializer) || !aliases.Add(serializer.ExactAlias)
                || StrictUtf8.GetByteCount(serializer.ExactAlias) > 512 * 1024
                || StrictUtf8.GetByteCount(serializer.Format) > 512 * 1024)
            {
                throw new ArgumentException("CapabilityMismatch: ambiguous or unbounded V1 serializer declaration.", nameof(serializers));
            }
        }

        _serializers = admitted.ToFrozenDictionary();
    }

    /// <summary>Snapshots and preflights the complete result before creating a payload sink or invoking any serializer.</summary>
    internal async Task<DomainServiceWireResult> ProduceAsync(DomainResult result, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(result);
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<IEventPayload> source = result.Events;
        int count = source.Count;
        if (count is < 0 or > 1000)
        {
            throw new InvalidOperationException("ResultLimit: a V1 domain result exceeds 1,000 events.");
        }

        // This bounded reference snapshot runs before any serializer callback.
        IEventPayload[] payloads = new IEventPayload[count];
        for (int index = 0; index < count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            payloads[index] = source[index] ?? throw new InvalidOperationException("CapabilityMismatch: null event payload.");
        }

        byte[]?[] serializedSources = new byte[]?[count];
        byte[]?[] privateSources = new byte[]?[count];
        BoundedV1EventSerialization[] declarations = new BoundedV1EventSerialization[count];
        long encoded = 128;
        long declaredPayloads = 0;
        long serializedSourceBytes = 0;
        long largestPayload = 0;
        bool isRejection = count > 0 && payloads[0] is IRejectionEvent;
        string? resultPayload = !isRejection ? result.ResultPayload : null;
        if (resultPayload is not null)
        {
            encoded = checked(encoded + 6L * StrictUtf8.GetByteCount(resultPayload) + 2);
        }

        for (int i = 0; i < count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IEventPayload payload = payloads[i];
            if (!_serializers.TryGetValue(payload.GetType(), out BoundedV1EventSerialization? serializer))
            {
                throw new InvalidOperationException("CapabilityMismatch: no explicitly bounded V1 serializer is declared for this payload.");
            }

            if ((payload is IRejectionEvent) != isRejection)
            {
                throw new InvalidOperationException("CapabilityMismatch: result mixes rejection and regular events.");
            }
            if (payload is ISerializedEventPayload serialized)
            {
                if (!string.Equals(serialized.EventTypeName, serializer.ExactAlias, StringComparison.Ordinal)
                    || !string.Equals(serialized.SerializationFormat, serializer.Format, StringComparison.Ordinal)
                    || serialized.MetadataVersion is not (null or 1) || serialized.EventContractType is not null
                    || serialized.PayloadVersion is not null)
                {
                    throw new InvalidOperationException("CapabilityMismatch: serialized V1 payload does not match its admitted exact alias/format.");
                }
                byte[] bytes = serialized.PayloadBytes ?? throw new InvalidOperationException("CapabilityMismatch: null serialized payload.");
                if (bytes.Length > serializer.MaximumPayloadBytes)
                {
                    throw new InvalidOperationException("PayloadLimit: admitted serialized V1 bytes exceed the declared bound.");
                }
                serializedSources[i] = bytes;
                serializedSourceBytes = checked(serializedSourceBytes + bytes.Length);
            }

            payloads[i] = payload;
            declarations[i] = serializer;
            long maximum = Math.Min(serializer.MaximumPayloadBytes, _readableLimit);
            declaredPayloads = checked(declaredPayloads + maximum);
            largestPayload = Math.Max(largestPayload, maximum);
            long metadata = checked(128 + 6L * StrictUtf8.GetByteCount(serializer.ExactAlias)
                + 6L * StrictUtf8.GetByteCount(serializer.Format));
            if (metadata > 512 * 1024)
            {
                throw new InvalidOperationException("MetadataLimit: encoded V1 event metadata exceeds 512 KiB.");
            }
            encoded = checked(encoded + metadata + 4 * ((maximum + 2) / 3));
        }

        long scratch = checked(2 * serializedSourceBytes + declaredPayloads + largestPayload + 128 * 1024L + count * 256L + (resultPayload?.Length ?? 0) * 2L);
        if (encoded > MaximumResultBytes || scratch > MaximumScratchBytes)
        {
            throw new InvalidOperationException("ResultLimit: complete declared V1 result or live workspace exceeds its admitted capacity.");
        }

        var events = new List<DomainServiceWireEvent>(count);
        try
        {
            // Every already-serialized source is private before the first serializer callback.
            // A callback can mutate its own objects, but cannot substitute later admitted bytes.
            for (int index = 0; index < count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                privateSources[index] = serializedSources[index]?.ToArray();
            }

            for (int i = 0; i < count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                BoundedV1EventSerialization serializer = declarations[i];
                using var sink = new BoundedV1PayloadStream(serializer.MaximumPayloadBytes, cancellationToken);
                if (privateSources[i] is byte[] serialized)
                {
                    sink.Write(serialized);
                }
                else
                {
                    await serializer.SerializeAsync(payloads[i], sink, cancellationToken).ConfigureAwait(false);
                }
                cancellationToken.ThrowIfCancellationRequested();
                events.Add(new DomainServiceWireEvent(serializer.ExactAlias, sink.SealAndCopy(), serializer.Format));
            }

            cancellationToken.ThrowIfCancellationRequested();
            return new DomainServiceWireResult(isRejection, events, resultPayload);
        }
        catch
        {
            foreach (DomainServiceWireEvent item in events) { CryptographicOperations.ZeroMemory(item.Payload); }
            throw;
        }
        finally
        {
            foreach (byte[]? sourceBytes in privateSources)
            {
                if (sourceBytes is not null) { CryptographicOperations.ZeroMemory(sourceBytes); }
            }
        }
    }
}
