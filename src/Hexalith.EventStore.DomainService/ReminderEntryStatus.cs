namespace Hexalith.EventStore.DomainService;

/// <summary>Persisted lifecycle of one reminder witness inside its item state.</summary>
internal enum ReminderEntryStatus
{
    /// <summary>The witness is persisted; the scheduler has not yet confirmed the reminder.</summary>
    Pending = 0,

    /// <summary>The witness is persisted and the scheduler accepted the reminder.</summary>
    Armed = 1,

    /// <summary>A submission outcome was uncertain or unavailable; the witness is retained and retried.</summary>
    Retrying = 2,

    /// <summary>The witness collided or failed admission and is retained for operator disposition.</summary>
    Quarantined = 3,
}
