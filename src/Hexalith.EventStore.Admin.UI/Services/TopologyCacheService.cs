using Hexalith.EventStore.Admin.Abstractions.Models.Tenants;
using Hexalith.EventStore.Admin.Abstractions.Models.TypeCatalog;

namespace Hexalith.EventStore.Admin.UI.Services;

/// <summary>
/// Caches tenant and domain topology data.
/// Fetches once on first access, refreshes only on DashboardRefreshService signal.
/// </summary>
public class TopologyCacheService(AdminStreamApiClient apiClient)
{
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
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
    /// Keeps stale cache on failure.
    /// </summary>
    public async Task RefreshAsync(CancellationToken ct = default)
    {
        if (!await _refreshLock.WaitAsync(0, ct).ConfigureAwait(false))
        {
            return; // Another refresh is already in progress
        }

        try
        {
            bool hadCachedTopology = _tenants is not null || _domains is not null;
            bool loadedTenants = false;
            bool loadedDomains = false;

            try
            {
                _tenants = await apiClient.GetTenantsAsync(ct).ConfigureAwait(false);
                loadedTenants = true;
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                // Best-effort sidebar data: keep stale tenants on HTTP timeout.
            }
            catch
            {
                // Best-effort sidebar data: keep stale tenants on failure.
            }

            try
            {
                IReadOnlyList<AggregateTypeInfo> types = await apiClient.GetAggregateTypesAsync(ct: ct).ConfigureAwait(false);
                _domains = types.Select(t => t.Domain).Distinct().OrderBy(d => d).ToList();
                loadedDomains = true;
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                // Best-effort sidebar data: keep stale domains on HTTP timeout.
            }
            catch
            {
                // Best-effort sidebar data: keep stale domains on failure.
            }

            _loaded = loadedTenants || loadedDomains || hadCachedTopology;
        }
        catch (OperationCanceledException)
        {
            throw; // Propagate cancellation
        }
        catch
        {
            // Keep stale cache on failure — topology refresh is best-effort
            _loaded = _tenants is not null;
        }
        finally
        {
            _refreshLock.Release();
        }
    }
}
