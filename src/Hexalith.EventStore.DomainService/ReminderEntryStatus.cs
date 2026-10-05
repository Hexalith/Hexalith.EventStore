namespace Hexalith.EventStore.DomainService;

/// <summary>Persisted lifecycle of one reminder witness inside its item state.</summary>
internal enum ReminderEntryStatus
{
    /// <summary>The witness is persisted and awaits successful Scheduler registration and its durable audit.</summary>
    Pending = 0,

    /// <summary>The witness is persisted and the scheduler accepted the reminder.</summary>
    Armed = 1,

    /// <summary>Submission, admission, audit, or cancellation work remains unresolved; the witness is retained and retried.</summary>
    Retrying = 2,

    /// <summary>The witness collided or its domain, translation, or effect evidence is invalid; it is retained for operator disposition.</summary>
    Quarantined = 3,
}
