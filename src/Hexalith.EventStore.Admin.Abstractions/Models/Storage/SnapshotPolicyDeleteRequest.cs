namespace Hexalith.EventStore.Admin.Abstractions.Models.Storage;

/// <summary>
/// Request payload for deleting an automatic snapshot policy.
/// </summary>
/// <param name="TenantId">Tenant identifier.</param>
/// <param name="Domain">Domain name.</param>
/// <param name="AggregateType">Aggregate type name.</param>
public sealed record SnapshotPolicyDeleteRequest(
    string TenantId,
    string Domain,
    string AggregateType);
