namespace Hexalith.EventStore.DomainService.DurableTasks;

/// <summary>The original admission and stable idempotency details for an owner step.</summary>
/// <param name="Task">The admitted task with the original Tenant, actor, operation and intent.</param>
/// <param name="Number">The one-based step number.</param>
/// <param name="Key">The idempotency key, stable across lease takeovers.</param>
/// <param name="Fence">The task's current lease generation.</param>
public sealed record DurableTaskEffectStep(CommittedDurableTaskAdmission Task, int Number, string Key, long Fence);
