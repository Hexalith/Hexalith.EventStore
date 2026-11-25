namespace Hexalith.EventStore.DomainService.DurableTasks;

/// <summary>The committed admission and progress of one Durable Task.</summary>
/// <param name="Task">The admitted task with its original bindings.</param>
/// <param name="Progress">The committed lifecycle state, checkpoint and owner receipts.</param>
public sealed record DurableTaskSnapshot(CommittedDurableTaskAdmission Task, DurableTaskProgress Progress);
