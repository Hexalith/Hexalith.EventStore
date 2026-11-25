namespace Hexalith.EventStore.DomainService.DurableTasks;

/// <summary>Identifies one idempotency scope for a durable task.</summary>
/// <param name="TenantId">The authoritative Tenant identifier.</param>
/// <param name="ActorId">The originating actor's stable identifier.</param>
/// <param name="Operation">The registered operation and contract version.</param>
/// <param name="IdempotencyKey">The caller's stable key within the Tenant, actor and operation.</param>
public sealed record DurableTaskScope(string TenantId, string ActorId, string Operation, string IdempotencyKey);
