using System.Security.Cryptography;

using Hexalith.EventStore.Client.Events;

namespace Hexalith.EventStore.Server.Events;

/// <summary>Holds one addressed replay range after the production reader has accepted it.</summary>
/// <remarks>Stored envelopes keep the actor payload. Domain envelopes are an in-memory view.</remarks>
internal sealed class DaprProductionLogicalReplay : IDisposable
{
    private readonly IReadOnlyList<EventBufferReservation> _domainReservations;
    /// <summary>Captures the pinned head and the stored and domain-facing envelopes.</summary>
    internal DaprProductionLogicalReplay(
        long actorHead,
        long retainedFloor,
        IReadOnlyList<EventEnvelope> storedEvents,
        IReadOnlyList<EventEnvelope> domainEvents,
        bool evolved,
        IReadOnlyList<EventBufferReservation> domainReservations)
    {
        ArgumentNullException.ThrowIfNull(storedEvents);
        ArgumentNullException.ThrowIfNull(domainEvents);
        ActorHead = actorHead;
        RetainedFloor = retainedFloor;
        StoredEvents = storedEvents;
        DomainEvents = domainEvents;
        Evolved = evolved;
        _domainReservations = domainReservations;
    }

    /// <summary>Gets the actor head pinned for every page in the range.</summary>
    internal long ActorHead { get; }

    /// <summary>Gets the retained floor pinned for every page in the range.</summary>
    internal long RetainedFloor { get; }

    /// <summary>Gets the stored envelopes. Their payload arrays are the actor values.</summary>
    internal IReadOnlyList<EventEnvelope> StoredEvents { get; }

    /// <summary>Gets unprotected or upcast envelopes for domain replay. These are not written back.</summary>
    internal IReadOnlyList<EventEnvelope> DomainEvents { get; }

    /// <summary>Gets whether any event needed an upcast hop.</summary>
    internal bool Evolved { get; }

    /// <summary>Clears private domain copies before releasing their live-buffer reservations.</summary>
    public void Dispose()
    {
        foreach (EventEnvelope envelope in DomainEvents)
        {
            CryptographicOperations.ZeroMemory(envelope.Payload);
        }

        foreach (EventBufferReservation reservation in _domainReservations)
        {
            reservation.Dispose();
        }
    }
}
