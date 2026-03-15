
using System.Text.Json;

using Dapr.Actors.Runtime;

using Hexalith.Commons.UniqueIds;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Contracts.Security;

using Microsoft.Extensions.Logging;

namespace Hexalith.EventStore.Server.Events;
/// <summary>
/// Persists domain events to the actor state store using write-once keys with gapless sequence numbers.
/// Created per-actor-call (same pattern as IdempotencyChecker, EventStreamReader).
/// Does NOT call SaveStateAsync -- the caller (AggregateActor) commits atomically (D1).
/// Storage key isolation (FR15, FR28): all keys are derived from AggregateIdentity which enforces
/// tenant-scoped composite keys. Cross-tenant access is structurally impossible because colons are
/// forbidden in identity components, ensuring disjoint key spaces per tenant.
/// SECURITY: Never use DaprClient.QueryStateAsync or bulk state queries without explicit tenant
/// filtering. DAPR query API does not enforce actor state scoping. See FR28.
/// </summary>
public partial class EventPersister(
    IActorStateManager stateManager,
    ILogger<EventPersister> logger,
    IEventPayloadProtectionService payloadProtectionService) : IEventPersister {
    /// <inheritdoc/>
    public async Task<EventPersistResult> PersistEventsAsync(
        AggregateIdentity identity,
        CommandEnvelope command,
        DomainResult domainResult,
        string domainServiceVersion) {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(domainResult);
        ArgumentException.ThrowIfNullOrWhiteSpace(domainServiceVersion);

        if (domainResult.Events.Count == 0) {
            return new EventPersistResult(0, []);
        }

        // Load current metadata to get sequence number
        ConditionalValue<AggregateMetadata> metadataResult = await stateManager
            .TryGetStateAsync<AggregateMetadata>(identity.MetadataKey)
            .ConfigureAwait(false);

        long currentSequence = metadataResult.HasValue ? metadataResult.Value.CurrentSequence : 0;

        string causationId = command.CausationId ?? command.CorrelationId;
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;
        _ = currentSequence + 1;
        var envelopes = new List<EventEnvelope>(domainResult.Events.Count);

        for (int i = 0; i < domainResult.Events.Count; i++) {
            IEventPayload eventPayload = domainResult.Events[i];
            long sequenceNumber = currentSequence + 1 + i;

            string eventTypeName = eventPayload is ISerializedEventPayload serializedPayload
                ? serializedPayload.EventTypeName
                : eventPayload.GetType().FullName ?? eventPayload.GetType().Name;
            byte[] payloadBytes = eventPayload is ISerializedEventPayload serialized
                ? serialized.PayloadBytes
                : JsonSerializer.SerializeToUtf8Bytes(eventPayload, eventPayload.GetType());
            string serializationFormat = eventPayload is ISerializedEventPayload serializedEvent
                ? serializedEvent.SerializationFormat
                : "json";

            PayloadProtectionResult protectionResult = await payloadProtectionService
                .ProtectEventPayloadAsync(
                    identity,
                    eventPayload,
                    eventTypeName,
                    payloadBytes,
                    serializationFormat)
                .ConfigureAwait(false);

            var envelope = new EventEnvelope(
                MessageId: UniqueIdHelper.GenerateSortableUniqueStringId(),
                AggregateId: identity.AggregateId,
                AggregateType: identity.Domain,
                TenantId: identity.TenantId,
                Domain: identity.Domain,
                SequenceNumber: sequenceNumber,
                GlobalPosition: 0,
                Timestamp: timestamp,
                CorrelationId: command.CorrelationId,
                CausationId: causationId,
                UserId: command.UserId,
                DomainServiceVersion: domainServiceVersion,
                EventTypeName: eventTypeName,
                MetadataVersion: 1,
                SerializationFormat: protectionResult.SerializationFormat,
                Payload: protectionResult.PayloadBytes,
                Extensions: null);

            envelopes.Add(envelope);

            string key = $"{identity.EventStreamKeyPrefix}{sequenceNumber}";

            Log.PersistingEvent(logger, key, eventTypeName, sequenceNumber);

            await stateManager
                .SetStateAsync(key, envelope)
                .ConfigureAwait(false);
        }

        // Update aggregate metadata with new sequence and timestamp
        long newSequence = currentSequence + domainResult.Events.Count;
        await stateManager
            .SetStateAsync(identity.MetadataKey, new AggregateMetadata(newSequence, timestamp, null))
            .ConfigureAwait(false);

        Log.EventsPersisted(logger, command.CorrelationId, causationId, identity.TenantId, identity.AggregateId, domainResult.Events.Count, newSequence);

        return new EventPersistResult(newSequence, envelopes);
    }

    private static partial class Log {
        [LoggerMessage(
            EventId = 3000,
            Level = LogLevel.Debug,
            Message = "Persisting event: Key={Key}, Type={EventTypeName}, Seq={Seq}")]
        public static partial void PersistingEvent(
            ILogger logger,
            string key,
            string eventTypeName,
            long seq);

        [LoggerMessage(
            EventId = 3001,
            Level = LogLevel.Information,
            Message = "Events persisted: CorrelationId={CorrelationId}, CausationId={CausationId}, TenantId={TenantId}, AggregateId={AggregateId}, EventCount={EventCount}, NewSequence={NewSequence}, Stage=EventsPersisted")]
        public static partial void EventsPersisted(
            ILogger logger,
            string correlationId,
            string causationId,
            string tenantId,
            string aggregateId,
            int eventCount,
            long newSequence);
    }
}
