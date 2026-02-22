
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Results;

namespace Hexalith.EventStore.Server.Events;
/// <summary>
/// Persists domain events to the actor state store using write-once keys with gapless sequence numbers.
/// </summary>
public interface IEventPersister {
    /// <summary>
    /// Persists events from a domain result to the actor state store.
    /// Assigns gapless sequence numbers and populates all 11 envelope metadata fields (SEC-1).
    /// Does NOT call SaveStateAsync -- the caller commits atomically (D1).
    /// </summary>
    /// <param name="identity">The aggregate identity providing key derivation.</param>
    /// <param name="command">The original command envelope for metadata extraction.</param>
    /// <param name="domainResult">The domain result containing event payloads.</param>
    /// <param name="domainServiceVersion">The version of the domain service that produced the events.</param>
    /// <returns>The persist result containing the new sequence number and persisted envelopes.</returns>
    Task<EventPersistResult> PersistEventsAsync(AggregateIdentity identity, CommandEnvelope command, DomainResult domainResult, string domainServiceVersion);
}
