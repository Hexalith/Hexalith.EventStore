using System.Text;

namespace Hexalith.EventStore.Server.Events;

/// <summary>Checks the retained legacy complete-array count, payload and conservative live-byte limits.</summary>
internal sealed class LegacyEventArrayBudget
{
    private const long MaximumPayloadBytes = 64L * 1024 * 1024;
    private const long MaximumAccountedBytes = 256L * 1024 * 1024;
    private const int MaximumEvents = 100_000;
    private const int PerEventCharge = 8192;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private long _payloadBytes;
    private long _accountedBytes;

    /// <summary>Reserves the required per-event accounting before allocating an event list.</summary>
    internal LegacyEventArrayBudget(long eventCount)
    {
        if (eventCount < 0 || eventCount > MaximumEvents
            || eventCount > MaximumAccountedBytes / PerEventCharge)
        {
            throw LimitExceeded();
        }

        _accountedBytes = checked(eventCount * PerEventCharge);
    }

    /// <summary>Charges each distinct stored payload and its encoded metadata before retaining it.</summary>
    internal void Add(EventEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        if (envelope.Payload is null || envelope.Payload.LongLength > MaximumPayloadBytes - _payloadBytes)
        {
            throw LimitExceeded();
        }

        long metadataBytes = 0;
        foreach (string? value in new[] {
            envelope.MessageId, envelope.AggregateId, envelope.AggregateType, envelope.TenantId,
            envelope.Domain, envelope.CorrelationId, envelope.CausationId, envelope.UserId,
            envelope.DomainServiceVersion, envelope.EventTypeName, envelope.SerializationFormat,
            envelope.EventContractType, envelope.ApplicationPayloadDigest,
        })
        {
            metadataBytes = checked(metadataBytes + StrictUtf8.GetByteCount(value ?? string.Empty));
        }

        if (envelope.Extensions is not null)
        {
            foreach ((string key, string value) in envelope.Extensions)
            {
                metadataBytes = checked(metadataBytes + StrictUtf8.GetByteCount(key)
                    + StrictUtf8.GetByteCount(value));
            }
        }

        long added = checked(envelope.Payload.LongLength + metadataBytes);
        if (added > MaximumAccountedBytes - _accountedBytes)
        {
            throw LimitExceeded();
        }

        _payloadBytes += envelope.Payload.LongLength;
        _accountedBytes += added;
    }

    private static InvalidOperationException LimitExceeded()
        => new("LegacyArrayLimit: complete event array exceeds its retained count or byte budget.");
}
