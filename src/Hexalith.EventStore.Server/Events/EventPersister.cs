
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
    IEventPayloadProtectionService payloadProtectionService,
    IGlobalPositionAllocator? globalPositionAllocator = null) : IEventPersister {
    private readonly IGlobalPositionAllocator _globalPositionAllocator = globalPositionAllocator ?? NoOpGlobalPositionAllocator.Instance;

    /// <inheritdoc/>
    public async Task<EventPersistResult> PersistEventsAsync(
        AggregateIdentity identity,
        string aggregateType,
        CommandEnvelope command,
        DomainResult domainResult,
        string domainServiceVersion,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentException.ThrowIfNullOrWhiteSpace(aggregateType);
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(domainResult);
        ArgumentException.ThrowIfNullOrWhiteSpace(domainServiceVersion);
        cancellationToken.ThrowIfCancellationRequested();

        if (domainResult.Events.Count == 0) {
            return new EventPersistResult(0, []);
        }

        var validatedPayloads = new List<(
            IEventPayload Payload,
            string EventTypeName,
            string SerializationFormat,
            string? EventContractType,
            int? PayloadVersion,
            int MetadataVersion)>(domainResult.Events.Count);
        foreach (IEventPayload eventPayload in domainResult.Events) {
            string eventTypeName = eventPayload is ISerializedEventPayload serializedPayload
                ? serializedPayload.EventTypeName
                : eventPayload.GetType().FullName ?? eventPayload.GetType().Name;
            string serializationFormat = eventPayload is ISerializedEventPayload serializedEvent
                ? serializedEvent.SerializationFormat
                : "json";
            string? eventContractType = eventPayload is ISerializedEventPayload versionedEvent
                ? versionedEvent.EventContractType
                : null;
            int? payloadVersion = eventPayload is ISerializedEventPayload versionedPayload
                ? versionedPayload.PayloadVersion
                : EventPayloadVersionResolver.GetDeclaredVersion(eventPayload.GetType()) is int declared and > 1
                    ? declared
                    : null;
            int metadataVersion = eventPayload is ISerializedEventPayload versionedMetadata
                ? versionedMetadata.MetadataVersion ?? 1
                : 1;

            if (eventPayload is ISerializedEventPayload serializedVersioned
                && eventPayload is not (SerializedDomainEventPayload or SerializedDomainRejectionEventPayload)
                && (serializedVersioned.PayloadVersion ?? 1) != EventPayloadVersionResolver.GetDeclaredVersion(eventPayload.GetType()))
            {
                throw new InvalidOperationException($"Serialized event {eventPayload.GetType().FullName} has a payload version different from its declaration.");
            }

            ValidateEventVersionMetadata(eventTypeName, metadataVersion, eventContractType, payloadVersion);
            if (payloadVersion is not null)
            {
                if (!string.Equals(serializationFormat, "json", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException($"Versioned event {eventTypeName} must use JSON.");
                }
                if (eventPayload is ISerializedEventPayload serializedJson)
                {
                    ValidateVersionedJsonObject(serializedJson.PayloadBytes, eventTypeName);
                }
            }
            if (metadataVersion == 2) {
                throw new InvalidOperationException("CapabilityMismatch: V2 writes require qualified negotiated writer authority.");
            }

            validatedPayloads.Add((eventPayload, eventTypeName, serializationFormat, eventContractType,
                payloadVersion == 1 ? null : payloadVersion, metadataVersion));
        }

        // Load current metadata to get sequence number
        ConditionalValue<AggregateMetadata> metadataResult = await stateManager
            .TryGetStateAsync<AggregateMetadata>(identity.MetadataKey, cancellationToken)
            .ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        long currentSequence = metadataResult.HasValue ? metadataResult.Value.CurrentSequence : 0;
        if (metadataResult.HasValue && currentSequence <= 0) {
            throw new InvalidOperationException("Invalid aggregate metadata: a stored CurrentSequence must be positive.");
        }

        long retainedFloor = metadataResult.HasValue ? metadataResult.Value.RetainedFloor : 1;
        if (retainedFloor < 1 || (retainedFloor > currentSequence && retainedFloor - currentSequence > 1))
        {
            throw new InvalidOperationException("SourceHeadChanged: actor metadata has an invalid retained floor.");
        }

        long newSequence = checked(currentSequence + domainResult.Events.Count);

        string causationId = command.CausationId ?? command.CorrelationId;
        var messageIds = new string[validatedPayloads.Count];
        for (int index = 0; index < validatedPayloads.Count; index++)
        {
            messageIds[index] = UniqueIdHelper.GenerateSortableUniqueStringId();
            EventIdentityValidator.ValidateForWrite(identity.TenantId, identity.Domain, identity.AggregateId,
                aggregateType, validatedPayloads[index].EventTypeName, messageIds[index],
                command.CorrelationId, causationId, checked(currentSequence + 1 + index));
        }
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;
        var preparedEvents = new List<(
            string EventTypeName,
            PayloadProtectionResult ProtectionResult,
            IDictionary<string, string> Extensions,
            string? EventContractType,
            int? PayloadVersion,
            int MetadataVersion,
            string ApplicationFormat,
            byte[] ApplicationPayloadHash)>(domainResult.Events.Count);
        var envelopes = new List<EventEnvelope>(domainResult.Events.Count);

        try {

            foreach ((IEventPayload eventPayload, string eventTypeName, string serializationFormat, string? eventContractType, int? payloadVersion, int metadataVersion) in validatedPayloads) {
                cancellationToken.ThrowIfCancellationRequested();
                byte[] payloadBytes = eventPayload is ISerializedEventPayload serialized
                    ? serialized.PayloadBytes
                    : JsonSerializer.SerializeToUtf8Bytes(eventPayload, eventPayload.GetType());
                if (payloadVersion is not null)
                {
                    ValidateVersionedJsonObject(payloadBytes, eventTypeName);
                }
                byte[] applicationPayloadHash = EventLogicalDigest.HashPayload(payloadBytes);

                try {
                    PayloadProtectionResult protectionResult = await payloadProtectionService
                        .ProtectEventPayloadAsync(
                            identity,
                            eventPayload,
                            eventTypeName,
                            payloadBytes,
                            serializationFormat,
                            cancellationToken)
                        .ConfigureAwait(false);

                    IDictionary<string, string> extensions = EventStorePayloadProtectionMetadataCarrier.Write(
                        extensions: (IDictionary<string, string>?)null,
                        metadata: protectionResult.Metadata);

                    preparedEvents.Add((eventTypeName, protectionResult, extensions, eventContractType, payloadVersion,
                        metadataVersion, serializationFormat, applicationPayloadHash));
                }
                catch {
                    System.Security.Cryptography.CryptographicOperations.ZeroMemory(applicationPayloadHash);
                    throw;
                }
            }

            long firstGlobalPosition = await _globalPositionAllocator
                .AllocateAsync(domainResult.Events.Count, cancellationToken)
                .ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            for (int i = 0; i < preparedEvents.Count; i++) {
                cancellationToken.ThrowIfCancellationRequested();
                (
                    string eventTypeName,
                    PayloadProtectionResult protectionResult,
                    IDictionary<string, string> extensions,
                    string? eventContractType,
                    int? payloadVersion,
                    int metadataVersion,
                    string applicationFormat,
                    byte[] applicationPayloadHash) = preparedEvents[i];
                long sequenceNumber = currentSequence + 1 + i;
                long globalPosition = firstGlobalPosition > 0
                    ? checked(firstGlobalPosition + i)
                    : 0;

                var envelope = new EventEnvelope(
                    MessageId: messageIds[i],
                    AggregateId: identity.AggregateId,
                    AggregateType: aggregateType,
                    TenantId: identity.TenantId,
                    Domain: identity.Domain,
                    SequenceNumber: sequenceNumber,
                    GlobalPosition: globalPosition,
                    Timestamp: timestamp,
                    CorrelationId: command.CorrelationId,
                    CausationId: causationId,
                    UserId: command.UserId,
                    DomainServiceVersion: domainServiceVersion,
                    EventTypeName: eventTypeName,
                    MetadataVersion: metadataVersion,
                    SerializationFormat: protectionResult.SerializationFormat,
                    Payload: protectionResult.PayloadBytes,
                    Extensions: extensions) {
                    EventContractType = eventContractType,
                    PayloadVersion = payloadVersion,
                };

                envelope = envelope with {
                    ApplicationPayloadDigest = EventLogicalDigest.Compute(envelope, applicationFormat, applicationPayloadHash),
                };

                envelopes.Add(envelope);

                string key = $"{identity.EventStreamKeyPrefix}{sequenceNumber}";

                Log.PersistingEvent(logger, key, eventTypeName, sequenceNumber);

                await stateManager
                    .SetStateAsync(key, envelope, cancellationToken)
                    .ConfigureAwait(false);
            }

            // Update aggregate metadata with new sequence and timestamp
            cancellationToken.ThrowIfCancellationRequested();
            await stateManager
                .SetStateAsync(identity.MetadataKey, new AggregateMetadata(
                    newSequence, timestamp, null, retainedFloor), cancellationToken)
                .ConfigureAwait(false);

            Log.EventsPersisted(logger, command.CorrelationId, causationId, identity.TenantId, identity.AggregateId, domainResult.Events.Count, newSequence);

            return new EventPersistResult(newSequence, envelopes);
        }
        finally {
            foreach (var prepared in preparedEvents) {
                System.Security.Cryptography.CryptographicOperations.ZeroMemory(prepared.ApplicationPayloadHash);
            }
        }
    }

    private static void ValidateEventVersionMetadata(
        string eventTypeName,
        int metadataVersion,
        string? eventContractType,
        int? payloadVersion) {
        if (metadataVersion == 1) {
            if (eventContractType is not null) {
                throw new ArgumentException("V1 event metadata cannot include EventContractType.", nameof(eventContractType));
            }
            if (payloadVersion is < 1 or > 1024) {
                throw new ArgumentOutOfRangeException(nameof(payloadVersion), payloadVersion,
                    "Payload version must be between 1 and 1024.");
            }

            return;
        }

        if (metadataVersion != 2) {
            throw new ArgumentOutOfRangeException(nameof(metadataVersion), metadataVersion, "Only event metadata versions 1 and 2 are supported.");
        }

        if (eventContractType is null || payloadVersion is null) {
            throw new ArgumentException("V2 event metadata requires both EventContractType and PayloadVersion.", nameof(eventContractType));
        }

        ValidateCanonicalEventContractType(eventContractType);
        if (payloadVersion is < 1 or > 1024) {
            throw new ArgumentOutOfRangeException(nameof(payloadVersion), payloadVersion, "Payload version must be between 1 and 1024.");
        }

        if (!string.Equals(eventTypeName, eventContractType, StringComparison.Ordinal)) {
            throw new ArgumentException("V2 EventTypeName must exactly match EventContractType.", nameof(eventTypeName));
        }
    }

    private static void ValidateVersionedJsonObject(byte[] payloadBytes, string eventTypeName)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(payloadBytes, new JsonDocumentOptions { MaxDepth = 64 });
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new JsonException("Event payload must be a JSON object.");
            }
        }
        catch (JsonException)
        {
            throw new InvalidOperationException($"Versioned event {eventTypeName} has invalid JSON.");
        }
    }

    private static void ValidateCanonicalEventContractType(string value) {
        if (value.Length is < 1 or > 64) {
            throw new ArgumentOutOfRangeException(nameof(value), value.Length, "Event contract type must contain 1 to 64 ASCII characters.");
        }

        for (int i = 0; i < value.Length; i++) {
            char character = value[i];
            bool alphaNumeric = character is >= 'a' and <= 'z' or >= '0' and <= '9';
            if (!alphaNumeric && (character != '-' || i == 0 || i == value.Length - 1)) {
                throw new ArgumentException("Event contract type must use canonical lower-case kebab-case.", nameof(value));
            }
        }
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
