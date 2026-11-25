namespace Hexalith.EventStore.DomainService.DurableTasks;

/// <summary>A worker's lease for an exact admitted task and fence.</summary>
/// <param name="Task">The task and its original admitted bindings.</param>
/// <param name="Owner">The worker lease identity.</param>
/// <param name="Fence">The lease generation.</param>
public sealed record DurableTaskLease(CommittedDurableTaskAdmission Task, string Owner, long Fence);
