namespace Hexalith.EventStore.SignalR;

/// <summary>
/// Metadata-only detail received from a projection-changed SignalR broadcast.
/// </summary>
/// <param name="ProjectionType">The projection type name.</param>
/// <param name="TenantId">The tenant identifier.</param>
/// <param name="GroupScope">Optional sub-tenant SignalR group scope.</param>
/// <param name="Metadata">Opaque bounded metadata supplied by the projection notification.</param>
public sealed record ProjectionChangedDetail(
    string ProjectionType,
    string TenantId,
    string? GroupScope,
    IReadOnlyDictionary<string, string> Metadata);
