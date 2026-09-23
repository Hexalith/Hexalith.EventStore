namespace Hexalith.EventStore.Client.Projections;

/// <summary>Durable outcome of an ordinary delivery attempting to cross the fence.</summary>
public enum SharedProjectionJournalResult
{
    /// <summary>The payload and checkpoint were atomically journaled.</summary>
    Journaled,
    /// <summary>The same position and digest were already accepted.</summary>
    AlreadyJournaled,
    /// <summary>The sealed inventory already includes this source position.</summary>
    AlreadyCaptured,
    /// <summary>The writer must refresh its lease and retry without acknowledgement.</summary>
    StaleLease,
    /// <summary>The bounded journal is full; retry after catch-up without acknowledgement.</summary>
    Backpressure,
    /// <summary>The same source position has a different canonical digest.</summary>
    IdentityConflict,
}
