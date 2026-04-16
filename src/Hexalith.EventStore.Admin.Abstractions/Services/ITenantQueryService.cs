using Hexalith.EventStore.Admin.Abstractions.Models.Tenants;

namespace Hexalith.EventStore.Admin.Abstractions.Services;

/// <summary>
/// Tenant queries routed through EventStore query pipeline.
/// EventStore does NOT own tenant state (FR77).
/// </summary>
public interface ITenantQueryService {
    /// <summary>
    /// Lists all tenants.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of tenant summaries.</returns>
    Task<IReadOnlyList<TenantSummary>> ListTenantsAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets detailed tenant information.
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
