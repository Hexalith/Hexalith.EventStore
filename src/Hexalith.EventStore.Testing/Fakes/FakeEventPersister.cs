
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Server.Events;

namespace Hexalith.EventStore.Testing.Fakes;
/// <summary>
/// Fake implementation of <see cref="IEventPersister"/> for unit testing.
/// Stores persisted events in memory for test assertions and supports configurable failure.
/// </summary>
public sealed class FakeEventPersister : IEventPersister {
    private readonly List<EventEnvelope> _persistedEvents = [];
    private readonly Dictionary<string, long> _sequenceByAggregate = [];
    private Exception? _exceptionToThrow;

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
    public Task<EventPersistResult> PersistEventsAsync(
        AggregateIdentity identity,
        CommandEnvelope command,
        DomainResult domainResult,
        string domainServiceVersion) {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(domainResult);
        ArgumentNullException.ThrowIfNull(domainServiceVersion);

        if (_exceptionToThrow is not null) {
            Exception ex = _exceptionToThrow;
            _exceptionToThrow = null;
            throw ex;
        }

        string aggregateKey = $"{identity.TenantId}:{identity.Domain}:{identity.AggregateId}";
        _ = _sequenceByAggregate.TryGetValue(aggregateKey, out long currentSequence);
        string causationId = command.CausationId ?? command.CorrelationId;
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;
        var envelopes = new List<EventEnvelope>();

        foreach (Hexalith.EventStore.Contracts.Events.IEventPayload eventPayload in domainResult.Events) {
            currentSequence++;
            string eventTypeName = eventPayload.GetType().FullName ?? eventPayload.GetType().Name;
            byte[] payloadBytes = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(eventPayload, eventPayload.GetType());

            var envelope = new EventEnvelope(
                MessageId: Guid.NewGuid().ToString(),
                AggregateId: identity.AggregateId,
                AggregateType: "unknown",
                TenantId: identity.TenantId,
                Domain: identity.Domain,
                SequenceNumber: currentSequence,
                GlobalPosition: 0,
                Timestamp: timestamp,
                CorrelationId: command.CorrelationId,
                CausationId: causationId,
                UserId: command.UserId,
                DomainServiceVersion: domainServiceVersion,
                EventTypeName: eventTypeName,
                MetadataVersion: 1,
                SerializationFormat: "json",
                Payload: payloadBytes,
                Extensions: null);

            _persistedEvents.Add(envelope);
            envelopes.Add(envelope);
        }

        _sequenceByAggregate[aggregateKey] = currentSequence;
        return Task.FromResult(new EventPersistResult(currentSequence, envelopes));
    }
}
