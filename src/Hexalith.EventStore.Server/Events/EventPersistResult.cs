namespace Hexalith.EventStore.Server.Events;

/// <summary>
/// Result of persisting events, containing the new sequence number and the persisted envelopes.
/// Story 4.1: EventPublisher needs the persisted envelopes to publish them via pub/sub.
/// </summary>
/// <param name="NewSequenceNumber">The new aggregate sequence number after persistence, or 0 if no events were persisted.</param>
/// <param name="PersistedEnvelopes">The persisted event envelopes with all 11 metadata fields populated.</param>
public record EventPersistResult(
    long NewSequenceNumber,
    IReadOnlyList<EventEnvelope> PersistedEnvelopes);
