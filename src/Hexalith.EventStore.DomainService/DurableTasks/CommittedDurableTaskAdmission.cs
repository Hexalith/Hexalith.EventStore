namespace Hexalith.EventStore.DomainService.DurableTasks;

/// <summary>The immutable identity and bindings of an admitted Durable Task.</summary>
/// <param name="TaskId">The task's platform-generated ULID.</param>
/// <param name="Scope">The original idempotency authority and operation bindings.</param>
/// <param name="RequestSha256">The normalized request hash.</param>
/// <param name="AdmittedAt">The time at which the admission was prepared for commit.</param>
public sealed record CommittedDurableTaskAdmission(
    string TaskId,
    DurableTaskScope Scope,
    string RequestSha256,
    DateTimeOffset AdmittedAt);
