namespace Hexalith.EventStore.SignalRHub;

/// <summary>
/// Broadcast request accepted only by Development-only SignalR proof endpoints.
/// </summary>
/// <param name="ProjectionType">Projection type to broadcast.</param>
/// <param name="TenantId">Tenant id to broadcast.</param>
/// <param name="GroupScope">Optional sub-tenant SignalR group scope.</param>
/// <param name="Metadata">Optional opaque metadata for a detail broadcast.</param>
public sealed record SignalRRuntimeProofBroadcastRequest(
    string ProjectionType,
    string TenantId,
    string? GroupScope = null,
    IReadOnlyDictionary<string, string>? Metadata = null);
