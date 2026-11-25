
using Hexalith.Commons.UniqueIds;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Contracts.Serialization;
using Hexalith.EventStore.Server.Events;

using EventEnvelope = Hexalith.EventStore.Server.Events.EventEnvelope;

namespace Hexalith.EventStore.Testing.Fakes;
/// <summary>
/// Fake implementation of <see cref="IEventPersister"/> for unit testing.
/// Stores persisted events in memory for test assertions and supports configurable failure.
/// </summary>
public sealed class FakeEventPersister : IEventPersister {
    private readonly NoOpEventPayloadProtectionService _payloadProtection = new();
    private readonly List<EventEnvelope> _persistedEvents = [];
    private readonly Dictionary<string, long> _sequenceByAggregate = [];
    private Exception? _exceptionToThrow;
    private long _nextGlobalPosition = 1;

    /// <summary>Gets the list of all persisted event envelopes for test assertions.</summary>
    public IReadOnlyList<EventEnvelope> PersistedEvents => _persistedEvents;

    /// <summary>
    /// Configures the fake to throw the specified exception on the next call to PersistEventsAsync.
    /// </summary>
    /// <param name="exception">The exception to throw.</param>
    public void SetupFailure(Exception exception) {
        ArgumentNullException.ThrowIfNull(exception);
        _exceptionToThrow = exception;
    }

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

        if (_exceptionToThrow is not null) {
            Exception ex = _exceptionToThrow;
            _exceptionToThrow = null;
            throw ex;
        }

        string aggregateKey = $"{identity.TenantId}:{identity.Domain}:{identity.AggregateId}";
        _ = _sequenceByAggregate.TryGetValue(aggregateKey, out long currentSequence);
        string causationId = command.CausationId ?? command.CorrelationId;
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;
        long nextGlobalPosition = _nextGlobalPosition;
        var envelopes = new List<EventEnvelope>();

        foreach (IEventPayload eventPayload in domainResult.Events) {
            currentSequence++;
            ISerializedEventPayload? serialized = eventPayload as ISerializedEventPayload;
            Type payloadType = eventPayload.GetType();
            string eventTypeName = serialized?.EventTypeName ?? payloadType.FullName ?? payloadType.Name;
            byte[] payloadBytes = EventStorePayloadSerialization.SerializeLegacyEventPayload(eventPayload);
            string serializationFormat = serialized?.SerializationFormat ?? "json";
            int payloadVersion = serialized is null
                ? EventPayloadVersionResolver.GetDeclaredVersion(payloadType)
                : serialized.PayloadVersion ?? 1;
            PayloadProtectionResult protection = await _payloadProtection.ProtectEventPayloadAsync(
                identity, eventPayload, eventTypeName, payloadBytes, serializationFormat, cancellationToken).ConfigureAwait(false);

            IDictionary<string, string> extensions = EventStorePayloadProtectionMetadataCarrier.Write(
                extensions: (IDictionary<string, string>?)null,
                metadata: protection.Metadata);

            var envelope = new EventEnvelope(
                MessageId: UniqueIdHelper.GenerateSortableUniqueStringId(),
                AggregateId: identity.AggregateId,
                AggregateType: aggregateType,
                TenantId: identity.TenantId,
                Domain: identity.Domain,
                SequenceNumber: currentSequence,
                GlobalPosition: nextGlobalPosition++,
                Timestamp: timestamp,
                CorrelationId: command.CorrelationId,
                CausationId: causationId,
                UserId: command.UserId,
                DomainServiceVersion: domainServiceVersion,
                EventTypeName: eventTypeName,
                MetadataVersion: 1,
                SerializationFormat: protection.SerializationFormat,
                Payload: serialized is null ? protection.PayloadBytes : protection.PayloadBytes.ToArray(),
                Extensions: extensions) {
                    PayloadVersion = payloadVersion == 1 ? null : payloadVersion,
                };

            envelopes.Add(envelope);
        }

        _persistedEvents.AddRange(envelopes);
        _nextGlobalPosition = nextGlobalPosition;
        _sequenceByAggregate[aggregateKey] = currentSequence;
        return new EventPersistResult(currentSequence, envelopes);
    }
}
