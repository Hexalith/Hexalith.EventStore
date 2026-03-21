namespace Hexalith.EventStore.Admin.Abstractions.Models.Tenants;

/// <summary>
/// Summary information about a tenant.
/// </summary>
/// <param name="TenantId">The tenant identifier.</param>
/// <param name="DisplayName">The tenant display name.</param>
/// <param name="Status">The current tenant status.</param>
/// <param name="EventCount">The total number of events for this tenant.</param>
/// <param name="DomainCount">The number of domains active in this tenant.</param>
public record TenantSummary(string TenantId, string DisplayName, TenantStatusType Status, long EventCount, int DomainCount)
{
    /// <summary>Gets the tenant identifier.</summary>
    public string TenantId { get; } = !string.IsNullOrWhiteSpace(TenantId)
        ? TenantId
        : throw new ArgumentException("TenantId cannot be null, empty, or whitespace.", nameof(TenantId));

    /// <summary>Gets the tenant display name.</summary>
    public string DisplayName { get; } = !string.IsNullOrWhiteSpace(DisplayName)
        ? DisplayName
        : throw new ArgumentException("DisplayName cannot be null, empty, or whitespace.", nameof(DisplayName));
}
