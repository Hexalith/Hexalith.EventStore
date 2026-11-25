namespace Hexalith.EventStore.DomainService.DurableTasks;

/// <summary>The trusted host adapter for authoritative lookup and idempotent submission to a step owner.</summary>
/// <remarks>
/// The owner must verify current authority for the original actor and the exact task-step binding on both
/// paths. The owner must atomically commit the effect and its receipt, deduplicating concurrent submissions
/// for the same key and rejecting a conflicting binding. An in-flight request may reach the owner after a lease
/// expires. Implementations must honor cancellation and surface unavailable or ambiguous outcomes as failures.
/// </remarks>
public interface IDurableTaskStepOwner
{
    /// <summary>Reads the terminal receipt, returning null only after an authoritative absence check.</summary>
    /// <param name="step">The exact committed step binding.</param>
    /// <param name="cancellationToken">The worker cancellation token.</param>
    /// <returns>The persisted receipt or authoritative absence.</returns>
    Task<DurableTaskOwnerReceipt?> LookupAsync(DurableTaskStep step, CancellationToken cancellationToken);

    /// <summary>Commits the effect and receipt together under the stable step key or returns the existing receipt.</summary>
    /// <param name="step">The exact committed step binding.</param>
    /// <param name="cancellationToken">The worker cancellation token.</param>
    /// <returns>The persisted terminal receipt.</returns>
    Task<DurableTaskOwnerReceipt> CommitAsync(DurableTaskStep step, CancellationToken cancellationToken);
}
