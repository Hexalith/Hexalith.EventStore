namespace Hexalith.EventStore.Admin.Abstractions.Models.Storage;

/// <summary>
/// Overview of event store storage usage (FR76).
/// </summary>
/// <param name="TotalEventCount">The total number of events across all tenants.</param>
/// <param name="TotalSizeBytes">The total storage size in bytes, or null if the state store backend does not support size queries (NFR44).</param>
/// <param name="TenantBreakdown">Per-tenant storage breakdown.</param>
public record StorageOverview(long TotalEventCount, long? TotalSizeBytes, IReadOnlyList<TenantStorageInfo> TenantBreakdown)
{
    /// <summary>Gets the per-tenant storage breakdown.</summary>
    public IReadOnlyList<TenantStorageInfo> TenantBreakdown { get; } = TenantBreakdown ?? throw new ArgumentNullException(nameof(TenantBreakdown));
}
