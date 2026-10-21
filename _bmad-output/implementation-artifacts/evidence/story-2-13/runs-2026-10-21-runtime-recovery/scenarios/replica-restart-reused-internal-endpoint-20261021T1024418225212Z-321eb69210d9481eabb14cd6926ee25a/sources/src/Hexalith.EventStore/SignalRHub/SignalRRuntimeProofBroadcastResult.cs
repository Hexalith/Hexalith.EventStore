namespace Hexalith.EventStore.SignalRHub;

/// <summary>
/// Broadcast result emitted by Development-only SignalR proof endpoints.
/// </summary>
/// <param name="ProjectionType">Projection type broadcast.</param>
/// <param name="TenantId">Tenant id broadcast.</param>
/// <param name="GroupScope">Optional sub-tenant SignalR group scope.</param>
/// <param name="GroupName">SignalR group name.</param>
/// <param name="MetadataCount">Number of metadata entries supplied to the broadcast.</param>
/// <param name="ProcessId">Origin process id.</param>
/// <param name="Timestamp">UTC timestamp after the broadcast call completed.</param>
public sealed record SignalRRuntimeProofBroadcastResult(
    string ProjectionType,
    string TenantId,
    string? GroupScope,
    string GroupName,
    int MetadataCount,
    int ProcessId,
    DateTimeOffset Timestamp);
