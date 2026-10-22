using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.EventStore.Server.Events;

/// <summary>Fences unsupported metadata while admitting versioned V1 payloads.</summary>
internal static class LegacyEventReadGuard
{
    /// <summary>Refuses unsupported metadata and invalid stored payload versions.</summary>
    internal static void RequireUnversioned(EventEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        EventIdentityValidator.ValidateForRead(envelope.TenantId, envelope.Domain, envelope.AggregateId,
            envelope.AggregateType, envelope.EventTypeName, envelope.MessageId,
            envelope.CorrelationId, envelope.CausationId, envelope.SequenceNumber);
        if (envelope.MetadataVersion != 1 || envelope.EventContractType is not null
            || envelope.PayloadVersion is < 1 or > 1024)
        {
            throw new InvalidOperationException("RollbackReaderCapabilityHold: a typed reader cannot consume unsupported event metadata.");
        }
    }
}
