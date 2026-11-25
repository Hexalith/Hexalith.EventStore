namespace Hexalith.EventStore.DomainService.DurableTasks;

/// <summary>The outcome of a cancellation request.</summary>
/// <param name="Outcome">The cancellation result class.</param>
/// <param name="Current">The committed task status, absent when the task was not found.</param>
public sealed record DurableTaskCancellationResult(DurableTaskCancellationOutcome Outcome, DurableTaskSnapshot? Current = null);
