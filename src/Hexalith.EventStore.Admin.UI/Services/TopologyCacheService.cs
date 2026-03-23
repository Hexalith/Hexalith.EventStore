using Hexalith.EventStore.Admin.Abstractions.Models.Tenants;
using Hexalith.EventStore.Admin.Abstractions.Models.TypeCatalog;

namespace Hexalith.EventStore.Admin.UI.Services;

/// <summary>
/// Caches tenant and domain topology data.
/// Fetches once on first access, refreshes only on DashboardRefreshService signal.
/// </summary>
public class TopologyCacheService(AdminStreamApiClient apiClient)
{
    private IReadOnlyList<TenantSummary>? _tenants;
    private IReadOnlyList<string>? _domains;
    private bool _loaded;

    /// <summary>
    /// Gets the cached tenants.
    /// </summary>
    public IReadOnlyList<TenantSummary> Tenants => _tenants ?? [];

    /// <summary>
    /// Gets the cached unique domain names.
    /// </summary>
    public IReadOnlyList<string> Domains => _domains ?? [];

    /// <summary>
    /// Gets whether the cache has been loaded at least once.
    /// </summary>
    public bool IsLoaded => _loaded;

    /// <summary>
    /// Loads topology data if not already cached.
    /// </summary>
    public async Task EnsureLoadedAsync(CancellationToken ct = default)
    {
        if (_loaded)
        {
            return;
        }

        await RefreshAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Forces a refresh of cached topology data.
    /// </summary>
    public async Task RefreshAsync(CancellationToken ct = default)
    {
        IReadOnlyList<TenantSummary> tenants = await apiClient.GetTenantsAsync(ct).ConfigureAwait(false);
        IReadOnlyList<AggregateTypeInfo> types = await apiClient.GetAggregateTypesAsync(ct: ct).ConfigureAwait(false);

        _tenants = tenants;
        _domains = types.Select(t => t.Domain).Distinct().OrderBy(d => d).ToList();
        _loaded = true;
    }
}
