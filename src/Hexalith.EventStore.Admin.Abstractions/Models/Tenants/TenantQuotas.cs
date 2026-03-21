namespace Hexalith.EventStore.Admin.Abstractions.Models.Tenants;

/// <summary>
/// Quota information for a tenant.
/// </summary>
/// <param name="TenantId">The tenant identifier.</param>
/// <param name="MaxEventsPerDay">The maximum number of events allowed per day.</param>
/// <param name="MaxStorageBytes">The maximum storage allowed in bytes.</param>
/// <param name="CurrentUsage">The current storage usage in bytes.</param>
public record TenantQuotas(string TenantId, long MaxEventsPerDay, long MaxStorageBytes, long CurrentUsage)
{
    /// <summary>Gets the tenant identifier.</summary>
    public string TenantId { get; } = !string.IsNullOrWhiteSpace(TenantId)
        ? TenantId
        : throw new ArgumentException("TenantId cannot be null, empty, or whitespace.", nameof(TenantId));
}
