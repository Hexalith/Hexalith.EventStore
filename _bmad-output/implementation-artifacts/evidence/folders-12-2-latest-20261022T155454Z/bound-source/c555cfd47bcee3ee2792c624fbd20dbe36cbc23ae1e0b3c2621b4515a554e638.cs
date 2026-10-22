namespace Hexalith.EventStore.Contracts.Reminders;

/// <summary>Audited outcome recorded for one reminder.</summary>
public enum ReminderDisposition
{
    /// <summary>The intent's witness is persisted and its reminder was armed.</summary>
    Registered,

    /// <summary>The target returned a durable receipt, first or replayed; witness release awaits successful Scheduler cancellation.</summary>
    Submitted,

    /// <summary>Submission is uncertain or unavailable, or audit/cancellation failed; retained work is retried.</summary>
    Retrying,

    /// <summary>The stream no longer reports the callback's witness; its command was not submitted and retirement was requested.</summary>
    Stale,

    /// <summary>Convergence found the witness obsolete and recorded cancellation intent; cleanup may still need retry.</summary>
    Cancelled,

    /// <summary>Callback admission was refused before submission; the pending state is retained.</summary>
    Denied,

    /// <summary>A tuple collision or malformed evidence was retained for operator disposition.</summary>
    Quarantined,
}
