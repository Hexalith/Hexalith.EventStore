using Hexalith.EventStore.Admin.Abstractions.Models.Tenants;

namespace Hexalith.EventStore.Admin.Abstractions.Services;

/// <summary>
/// Tenant queries delegated to Hexalith.Tenants Client SDK at implementation time (Admin.Server).
/// EventStore does NOT own tenant state (FR77).
/// </summary>
public interface ITenantQueryService
{
    /// <summary>
    /// Lists all tenants.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of tenant summaries.</returns>
    Task<IReadOnlyList<TenantSummary>> ListTenantsAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets the quota information for a specific tenant.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The tenant quotas.</returns>
    Task<TenantQuotas> GetTenantQuotasAsync(string tenantId, CancellationToken ct = default);

    /// <summary>
    /// Compares usage across multiple tenants.
    /// </summary>
    /// <param name="tenantIds">The tenant identifiers to compare.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The tenant comparison.</returns>
    Task<TenantComparison> CompareTenantUsageAsync(IReadOnlyList<string> tenantIds, CancellationToken ct = default);

    /// <summary>
    /// Gets detailed tenant information including quotas.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The tenant detail, or null if not found.</returns>
    Task<TenantDetail?> GetTenantDetailAsync(string tenantId, CancellationToken ct = default);

    /// <summary>
    /// Gets users assigned to a tenant.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of tenant users.</returns>
    Task<IReadOnlyList<TenantUser>> GetTenantUsersAsync(string tenantId, CancellationToken ct = default);
}
