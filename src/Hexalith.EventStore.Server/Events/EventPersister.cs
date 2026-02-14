namespace Hexalith.EventStore.Server.Events;

using System.Text.Json;

using Dapr.Actors.Runtime;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Results;

using Microsoft.Extensions.Logging;

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
public class EventPersister(
    IActorStateManager stateManager,
    ILogger<EventPersister> logger) : IEventPersister
{
    /// <inheritdoc/>
    public async Task<long> PersistEventsAsync(
        AggregateIdentity identity,
        CommandEnvelope command,
        DomainResult domainResult,
        string domainServiceVersion)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(domainResult);
        ArgumentException.ThrowIfNullOrWhiteSpace(domainServiceVersion);

        if (domainResult.Events.Count == 0)
        {
            return 0;
        }

        // Load current metadata to get sequence number
        ConditionalValue<AggregateMetadata> metadataResult = await stateManager
            .TryGetStateAsync<AggregateMetadata>(identity.MetadataKey)
            .ConfigureAwait(false);

        long currentSequence = metadataResult.HasValue ? metadataResult.Value.CurrentSequence : 0;

        string causationId = command.CausationId ?? command.CorrelationId;
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;
        long firstSeq = currentSequence + 1;

        for (int i = 0; i < domainResult.Events.Count; i++)
        {
            IEventPayload eventPayload = domainResult.Events[i];
            long sequenceNumber = currentSequence + 1 + i;

            string eventTypeName = eventPayload.GetType().FullName ?? eventPayload.GetType().Name;
            byte[] payloadBytes = JsonSerializer.SerializeToUtf8Bytes(eventPayload, eventPayload.GetType());

            var envelope = new EventEnvelope(
                AggregateId: identity.AggregateId,
                TenantId: identity.TenantId,
                Domain: identity.Domain,
                SequenceNumber: sequenceNumber,
                Timestamp: timestamp,
                CorrelationId: command.CorrelationId,
                CausationId: causationId,
                UserId: command.UserId,
                DomainServiceVersion: domainServiceVersion,
                EventTypeName: eventTypeName,
                SerializationFormat: "json",
                Payload: payloadBytes,
                Extensions: null);

            string key = $"{identity.EventStreamKeyPrefix}{sequenceNumber}";

            logger.LogDebug(
                "Persisting event: Key={Key}, Type={EventTypeName}, Seq={Seq}",
                key,
                eventTypeName,
                sequenceNumber);

            await stateManager
                .SetStateAsync(key, envelope)
                .ConfigureAwait(false);
        }

        // Update aggregate metadata with new sequence and timestamp
        long newSequence = currentSequence + domainResult.Events.Count;
        await stateManager
            .SetStateAsync(identity.MetadataKey, new AggregateMetadata(newSequence, timestamp, null))
            .ConfigureAwait(false);

        logger.LogInformation(
            "Events persisted: Count={EventCount}, Sequences={FirstSeq}-{LastSeq}, AggregateId={ActorId}, CorrelationId={CorrelationId}",
            domainResult.Events.Count,
            firstSeq,
            newSequence,
            identity.ActorId,
            command.CorrelationId);

        return newSequence;
    }
}
