namespace Hexalith.EventStore.DomainService.DurableTasks;

/// <summary>Result classes for a Durable Task cancellation request.</summary>
public enum DurableTaskCancellationOutcome
{
    /// <summary>The task is cancelled. Repeated requests return the same outcome.</summary>
    Cancelled,

    /// <summary>The task is terminal, past its irreversible checkpoint, or has an owner step in flight.</summary>
    Conflict,

    /// <summary>The task is absent or the caller lacks current authority for its original scope.</summary>
    NotFound,
}
