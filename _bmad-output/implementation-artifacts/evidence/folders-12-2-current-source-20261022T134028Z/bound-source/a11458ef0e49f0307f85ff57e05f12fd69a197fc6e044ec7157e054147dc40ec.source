namespace Hexalith.EventStore.Admin.Abstractions.Models.Storage;

/// <summary>
/// Request payload for the EventStore upstream manual snapshot route.
/// </summary>
/// <remarks>
/// <para>
/// The caller-supplied <see cref="CorrelationId"/> is forwarded for log/trace correlation only.
/// It MUST NOT be used as the successful idempotence key — the successful operation id is derived
/// deterministically from canonical tenant, canonical domain, aggregate id, and current sequence
/// (see DW16 story §Implementation Decisions).
/// </para>
/// </remarks>
/// <param name="TenantId">Tenant identifier. Will be canonicalized server-side.</param>
/// <param name="Domain">Domain name. Will be canonicalized server-side.</param>
/// <param name="AggregateId">Aggregate identifier.</param>
/// <param name="CorrelationId">Optional caller correlation id for log/trace correlation only.</param>
public record ManualSnapshotRequest(
    string TenantId,
    string Domain,
    string AggregateId,
    string? CorrelationId = null);
