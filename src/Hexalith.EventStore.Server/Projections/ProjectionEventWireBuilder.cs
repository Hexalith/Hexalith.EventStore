using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Events;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>Builds protected-data-safe projection wire events from persisted event envelopes.</summary>
internal static class ProjectionEventWireBuilder {
    /// <summary>Unprotects readable events without exposing server-internal metadata on the wire.</summary>
    /// <param name="payloadProtectionService">The configured payload protection provider.</param>
    /// <param name="identity">The owning aggregate identity.</param>
    /// <param name="events">The persisted event envelopes.</param>
    /// <param name="cancellationToken">Propagates cancellation.</param>
    /// <returns>The readable wire sequence or one bounded unreadable result.</returns>
    public static async Task<ProjectionEventReadabilityResult> BuildAsync(
        IEventPayloadProtectionService payloadProtectionService,
        AggregateIdentity identity,
        IReadOnlyList<EventEnvelope> events,
        CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(payloadProtectionService);
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(events);

        // Validate the complete addressed batch before invoking a protection provider
        // or constructing any handler-facing projection event.
        long previousSequence = 0;
        for (int i = 0; i < events.Count; i++) {
            EventEnvelope envelope = events[i]
                ?? throw new InvalidOperationException("AddressMismatch: projection batch contains a null event.");
            if (!string.Equals(envelope.TenantId, identity.TenantId, StringComparison.Ordinal)
                || !string.Equals(envelope.Domain, identity.Domain, StringComparison.Ordinal)
                || !string.Equals(envelope.AggregateId, identity.AggregateId, StringComparison.Ordinal)
                || envelope.SequenceNumber <= 0
                || envelope.Payload is null
                || (i > 0 && (previousSequence == long.MaxValue || envelope.SequenceNumber != previousSequence + 1))) {
                throw new InvalidOperationException("AddressMismatch: projection batch does not match its addressed contiguous stream.");
            }

            LegacyEventReadGuard.RequireUnversioned(envelope);
            previousSequence = envelope.SequenceNumber;
        }

        var projectionEvents = new ProjectionEventDto[events.Count];
        for (int i = 0; i < events.Count; i++) {
            EventEnvelope envelope = events[i];
            cancellationToken.ThrowIfCancellationRequested();
            EventStorePayloadProtectionMetadata storedMetadata = EventStorePayloadProtectionMetadataCarrier
                .Read(envelope.Extensions);
            if (storedMetadata.State == PayloadProtectionState.ProviderOpaque) {
                return ProjectionEventReadabilityResult.Unreadable(
                    UnreadableProtectedDataReasonMapper.FromProviderOpaqueMetadata(storedMetadata),
                    envelope.SequenceNumber);
            }

            byte[] payload = envelope.Payload;
            string serializationFormat = envelope.SerializationFormat;
            if (storedMetadata.State == PayloadProtectionState.Protected) {
                PayloadUnprotectionOutcome outcome;
                try {
                    outcome = await payloadProtectionService
                        .TryUnprotectEventPayloadAsync(
                            identity,
                            envelope.EventTypeName,
                            envelope.Payload,
                            envelope.SerializationFormat,
                            storedMetadata,
                            cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException) {
                    throw;
                }
                catch (Exception) {
                    return ProjectionEventReadabilityResult.Unreadable(
                        UnreadableProtectedDataReason.ProviderUnavailable,
                        envelope.SequenceNumber);
                }

                if (outcome.IsUnreadable) {
                    return ProjectionEventReadabilityResult.Unreadable(
                        outcome.UnreadableReason!.Value,
                        envelope.SequenceNumber);
                }

                payload = outcome.PayloadBytes!;
                serializationFormat = outcome.SerializationFormat!;
            }

            EventLogicalDigest.RequireMatching(envelope, serializationFormat, payload);

            projectionEvents[i] = new ProjectionEventDto(
                envelope.EventTypeName,
                payload,
                serializationFormat,
                envelope.SequenceNumber,
                envelope.Timestamp,
                envelope.CorrelationId,
                envelope.MessageId,
                envelope.UserId,
                envelope.GlobalPosition);
        }

        return ProjectionEventReadabilityResult.Readable(projectionEvents);
    }
}
