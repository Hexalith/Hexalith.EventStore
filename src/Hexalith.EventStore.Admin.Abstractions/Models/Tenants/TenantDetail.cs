namespace Hexalith.EventStore.Admin.Abstractions.Models.Tenants;

/// <summary>
/// Detailed tenant information including quota and configuration.
/// </summary>
/// <param name="TenantId">The tenant identifier.</param>
/// <param name="DisplayName">The tenant display name.</param>
/// <param name="Status">The current tenant status.</param>
/// <param name="EventCount">Total events for this tenant.</param>
/// <param name="DomainCount">Number of active domains.</param>
/// <param name="StorageBytes">Current storage usage in bytes.</param>
/// <param name="CreatedAtUtc">When the tenant was created.</param>
/// <param name="Quotas">Quota configuration, null if not set.</param>
/// <param name="SubscriptionTier">Subscription tier name.</param>
public record TenantDetail(
    string TenantId,
    string DisplayName,
    TenantStatusType Status,
    long EventCount,
    int DomainCount,
    long StorageBytes,
    DateTimeOffset CreatedAtUtc,
    TenantQuotas? Quotas,
    string? SubscriptionTier)
{
    /// <summary>Gets the tenant identifier.</summary>
    public string TenantId { get; } = !string.IsNullOrWhiteSpace(TenantId)
        ? TenantId
        : throw new ArgumentException("TenantId cannot be null, empty, or whitespace.", nameof(TenantId));

    /// <summary>Gets the tenant display name.</summary>
    public string DisplayName { get; } = DisplayName ?? string.Empty;
}
