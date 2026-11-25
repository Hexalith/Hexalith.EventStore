namespace Hexalith.EventStore.DomainService.DurableTasks;

/// <summary>The committed lifecycle state of a Durable Task.</summary>
/// <remarks>Rows persist the member name. A missing value reads as <see cref="Unknown"/>, and a persisted state outside this graph fails closed when read.</remarks>
public enum DurableTaskStatus
{
    /// <summary>No committed state. Readers reject a persisted row carrying this value.</summary>
    Unknown = 0,

    /// <summary>The task is admitted and no worker has started it.</summary>
    Pending,

    /// <summary>A worker lease may advance the next step from the committed checkpoint.</summary>
    Running,

    /// <summary>An owner call failed and the pending step resumes through authoritative lookup until the dependency failure budget is exhausted.</summary>
    WaitingForDependency,

    /// <summary>An unresolved partial owner effect or an exhausted dependency failure budget fences automatic execution pending reconciliation.</summary>
    NeedsAttention,

    /// <summary>Terminal: the workflow confirmed its authoritative end state.</summary>
    Succeeded,

    /// <summary>Terminal: a known domain or owner denial, including an owner's authoritative refusal of the pending step.</summary>
    Rejected,

    /// <summary>Terminal: a known non-retryable technical failure.</summary>
    Failed,

    /// <summary>Terminal: cancelled before the irreversible checkpoint. A cancellation from NeedsAttention keeps the pending step and committed receipts for later reconciliation.</summary>
    Cancelled,
}
