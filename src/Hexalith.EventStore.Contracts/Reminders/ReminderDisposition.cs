namespace Hexalith.EventStore.Contracts.Reminders;

/// <summary>Audited outcome recorded for one reminder.</summary>
public enum ReminderDisposition
{
    /// <summary>The intent's witness is persisted and its reminder was armed.</summary>
    Registered,

    /// <summary>The target returned a durable receipt, first or replayed, and the pending state was released.</summary>
    Submitted,

    /// <summary>The outcome is uncertain or submission is unavailable; the pending state is retained and retried.</summary>
    Retrying,

    /// <summary>The callback carried a witness the stream no longer reports; nothing was submitted and the reminder was cancelled.</summary>
    Stale,

    /// <summary>Convergence found the intent obsolete and cancelled its reminder.</summary>
    Cancelled,

    /// <summary>Callback admission was refused before submission; the pending state is retained.</summary>
    Denied,

    /// <summary>A tuple collision or malformed evidence was retained for operator disposition.</summary>
    Quarantined,
}
