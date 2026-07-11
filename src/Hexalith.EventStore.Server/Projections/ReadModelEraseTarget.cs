namespace Hexalith.EventStore.Server.Projections;

/// <summary>
/// Identifies one tenant-owned read-model key participating in a coordinated projection erase.
/// </summary>
/// <param name="TenantId">The tenant that owns the target.</param>
/// <param name="StoreName">The DAPR state-store component name.</param>
/// <param name="Key">The read-model state key, prefixed with <c>{TenantId}:</c>.</param>
/// <param name="ETag">The expected read-model ETag.</param>
public sealed record ReadModelEraseTarget(
    string TenantId,
    string StoreName,
    string Key,
    string ETag);
