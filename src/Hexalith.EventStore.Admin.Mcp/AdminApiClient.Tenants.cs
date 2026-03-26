namespace Hexalith.EventStore.Admin.Mcp;

using Hexalith.EventStore.Admin.Abstractions.Models.Tenants;

/// <summary>
/// AdminApiClient partial — tenant query methods.
/// </summary>
internal sealed partial class AdminApiClient
{
    /// <summary>
    /// Lists all tenants.
    /// </summary>
    public async Task<IReadOnlyList<TenantSummary>> ListTenantsAsync(CancellationToken cancellationToken)
    {
        return await GetListAsync<TenantSummary>("/api/v1/admin/tenants", cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets detailed tenant information.
    /// </summary>
    public async Task<TenantDetail?> GetTenantDetailAsync(string tenantId, CancellationToken cancellationToken)
    {
        string path = $"/api/v1/admin/tenants/{Uri.EscapeDataString(tenantId)}";
        return await GetAsync<TenantDetail>(path, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets tenant quotas.
    /// </summary>
    public async Task<TenantQuotas?> GetTenantQuotasAsync(string tenantId, CancellationToken cancellationToken)
    {
        string path = $"/api/v1/admin/tenants/{Uri.EscapeDataString(tenantId)}/quotas";
        return await GetAsync<TenantQuotas>(path, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets users assigned to a tenant.
    /// </summary>
    public async Task<IReadOnlyList<TenantUser>> GetTenantUsersAsync(string tenantId, CancellationToken cancellationToken)
    {
        string path = $"/api/v1/admin/tenants/{Uri.EscapeDataString(tenantId)}/users";
        return await GetListAsync<TenantUser>(path, cancellationToken).ConfigureAwait(false);
    }
}
