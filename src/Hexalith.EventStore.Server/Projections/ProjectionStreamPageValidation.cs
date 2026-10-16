using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.Server.Events;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>Validates an actor stream page before a projection reader advances its exclusive cursor.</summary>
internal static class ProjectionStreamPageValidation
{
    /// <summary>Classifies the new bounded guard/replay protocol without changing released reader diagnostics.</summary>
    internal static long ValidateGuarded(AggregateIdentity identity, long fromSequence, long throughSequence,
        int pageSize, EventEnvelope[]? envelopes)
    {
        if (envelopes is null || envelopes.Length == 0)
            throw new ProjectionDeliveryHistoryValidationException("delivery_gap");
        if (envelopes.Length > pageSize)
            throw new ProjectionDeliveryHistoryValidationException("delivery_identity_conflict");
        long expected = fromSequence;
        foreach (EventEnvelope? envelope in envelopes)
        {
            if (envelope is null || envelope.TenantId != identity.TenantId || envelope.Domain != identity.Domain
                || envelope.AggregateId != identity.AggregateId || envelope.Payload is null
                || string.IsNullOrWhiteSpace(envelope.MessageId) || string.IsNullOrWhiteSpace(envelope.EventTypeName)
                || string.IsNullOrWhiteSpace(envelope.SerializationFormat) || envelope.SequenceNumber <= expected)
                throw new ProjectionDeliveryHistoryValidationException("delivery_identity_conflict");
            if (expected == long.MaxValue || envelope.SequenceNumber != expected + 1 || envelope.SequenceNumber > throughSequence)
                throw new ProjectionDeliveryHistoryValidationException("delivery_gap");
            expected = envelope.SequenceNumber;
        }
        return expected;
    }

    /// <summary>Checks one actor page against the requested aggregate and returns its exclusive cursor.</summary>
    internal static long Validate(
        AggregateIdentity identity,
        long fromSequence,
        long throughSequence,
        int pageSize,
        EventEnvelope[] envelopes)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(envelopes);
        var request = new StreamReadRequest(
            identity.TenantId,
            identity.Domain,
            identity.AggregateId,
            fromSequence,
            throughSequence,
            PageSize: pageSize);
        var events = new StreamReadEvent[envelopes.Length];
        long expected = fromSequence;
        for (int index = 0; index < envelopes.Length; index++)
        {
            EventEnvelope envelope = envelopes[index]
                ?? throw new ProjectionDeliveryHistoryValidationException("Authoritative projection history contains a null envelope.");
            if (!string.Equals(envelope.TenantId, identity.TenantId, StringComparison.Ordinal)
                || !string.Equals(envelope.Domain, identity.Domain, StringComparison.Ordinal)
                || !string.Equals(envelope.AggregateId, identity.AggregateId, StringComparison.Ordinal)
                || envelope.SequenceNumber != checked(expected + 1))
            {
                throw new ProjectionDeliveryHistoryValidationException("Authoritative projection history has an invalid identity or position.");
            }

            expected = envelope.SequenceNumber;
            events[index] = new StreamReadEvent(
                envelope.SequenceNumber,
                envelope.EventTypeName,
                envelope.Payload,
                envelope.SerializationFormat,
                envelope.MetadataVersion,
                envelope.MessageId,
                envelope.CorrelationId,
                envelope.CausationId,
                envelope.Timestamp,
                envelope.UserId)
            {
                PayloadVersion = envelope.PayloadVersion,
                GlobalPosition = envelope.GlobalPosition,
            };
        }

        long latest = Math.Max(throughSequence, fromSequence);
        var page = new StreamReadPage(
            identity.TenantId,
            identity.Domain,
            identity.AggregateId,
            events,
            new StreamReadMetadata(
                fromSequence,
                throughSequence,
                envelopes.Length == 0 ? null : expected,
                latest,
                envelopes.Length,
                expected < throughSequence,
                null));
        try
        {
            return StreamReadPageValidator.ValidateAndGetNextSequence(request, page);
        }
        catch (InvalidOperationException exception)
        {
            throw new ProjectionDeliveryHistoryValidationException(exception.Message);
        }
    }
}
