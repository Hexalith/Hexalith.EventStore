using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.EventStore.DomainService;

/// <summary>Declares the exact serializers admitted for one domain's bounded V1 result writer.</summary>
/// <remarks>A serializer profile selects local bounded writing only; it does not advertise negotiated writer readiness.</remarks>
public sealed class BoundedV1DomainSerializerProfile
{
    private readonly List<BoundedV1EventSerialization> _serializers = [];

    /// <summary>Adds a once-only serializer with an exact legacy alias, format and byte ceiling.</summary>
    public BoundedV1DomainSerializerProfile Add<TPayload>(
        string exactAlias,
        string format,
        int maximumPayloadBytes,
        Func<TPayload, Stream, CancellationToken, Task> serializeAsync)
        where TPayload : IEventPayload
    {
        ArgumentNullException.ThrowIfNull(serializeAsync);
        _serializers.Add(new BoundedV1EventSerialization(typeof(TPayload), exactAlias, format, maximumPayloadBytes,
            (payload, stream, token) => serializeAsync((TPayload)payload, stream, token)));
        return this;
    }

    internal BoundedV1DomainResultProducer Build(int measuredReadableLimit)
        => new(_serializers, measuredReadableLimit);
}
