using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.Server.Events;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>Validates an actor stream page before a projection reader advances its exclusive cursor.</summary>
internal static class ProjectionStreamPageValidation
{
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
                envelope.UserId);
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
