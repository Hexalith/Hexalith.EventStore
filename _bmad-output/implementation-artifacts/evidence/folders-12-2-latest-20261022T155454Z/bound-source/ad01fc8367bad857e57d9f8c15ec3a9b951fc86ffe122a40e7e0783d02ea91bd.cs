namespace Hexalith.EventStore.Server.Commands;

/// <summary>Payload-free metadata for a privileged trusted-effect audit entry.</summary>
/// <param name="Action">The bounded privileged action code.</param>
/// <param name="Tenant">Tenant coordinate, when validation established it.</param>
/// <param name="EffectId">Canonical effect identifier, when validation established it.</param>
/// <param name="Workload">Attested caller workload, when available.</param>
/// <param name="Purpose">Delegated purpose, when available.</param>
/// <param name="Disposition">Allowed, denied, collision, or erasure-started.</param>
public sealed record TrustedEffectAuditRecord(
    string Action,
    string? Tenant,
    string? EffectId,
    string? Workload,
    string? Purpose,
    string Disposition);
