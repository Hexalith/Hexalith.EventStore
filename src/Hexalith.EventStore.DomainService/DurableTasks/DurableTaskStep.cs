namespace Hexalith.EventStore.DomainService.DurableTasks;

/// <summary>Identifies an owner operation bound to the original admitted intent.</summary>
/// <param name="Task">The persisted task, including its Tenant, original actor, operation and intent.</param>
/// <param name="Name">The registered step name, stable across worker instances.</param>
/// <param name="Key">The deterministic idempotency key sent to the owner.</param>
public sealed record DurableTaskStep(CommittedDurableTaskAdmission Task, string Name, string Key);
