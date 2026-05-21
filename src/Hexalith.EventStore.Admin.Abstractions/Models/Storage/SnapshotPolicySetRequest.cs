namespace Hexalith.EventStore.Admin.Abstractions.Models.Storage;

/// <summary>
/// Request payload for setting an automatic snapshot policy.
/// </summary>
/// <param name="TenantId">Tenant identifier.</param>
/// <param name="Domain">Domain name.</param>
/// <param name="AggregateType">Aggregate type name.</param>
/// <param name="IntervalEvents">Number of events between automatic snapshots.</param>
public sealed record SnapshotPolicySetRequest(
    string TenantId,
    string Domain,
    string AggregateType,
    int IntervalEvents);
