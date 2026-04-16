
using Hexalith.EventStore.Admin.Abstractions.Models.Health;
using Hexalith.EventStore.Admin.Abstractions.Models.Storage;

namespace Hexalith.EventStore.Admin.Mcp;
/// <summary>
/// AdminApiClient partial — storage and DAPR health query methods.
/// </summary>
internal sealed partial class AdminApiClient {
    /// <summary>
    /// Gets DAPR infrastructure component health status.
    /// </summary>
    public async Task<IReadOnlyList<DaprComponentHealth>> GetDaprComponentStatusAsync(
        CancellationToken cancellationToken) => await GetListAsync<DaprComponentHealth>("/api/v1/admin/health/dapr", cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Gets storage usage overview including event counts, sizes, and per-tenant breakdown.
    /// </summary>
    public async Task<StorageOverview?> GetStorageOverviewAsync(
        string? tenantId,
        CancellationToken cancellationToken) {
        string path = "/api/v1/admin/storage/overview";
        if (!string.IsNullOrEmpty(tenantId)) {
            path += $"?tenantId={Uri.EscapeDataString(tenantId)}";
        }

        return await GetAsync<StorageOverview>(path, cancellationToken).ConfigureAwait(false);
    }
}
