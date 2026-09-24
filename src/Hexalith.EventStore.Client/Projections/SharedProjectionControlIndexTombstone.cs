namespace Hexalith.EventStore.Client.Projections;

/// <summary>Audited, durable refusal to rediscover an offboarded tenant.</summary>
internal sealed record SharedProjectionControlIndexTombstone(string IndexName, string TenantId, string AuditId);
