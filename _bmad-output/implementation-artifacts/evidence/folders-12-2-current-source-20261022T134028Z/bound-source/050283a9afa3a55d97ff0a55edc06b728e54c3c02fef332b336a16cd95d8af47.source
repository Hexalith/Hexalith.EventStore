
using Hexalith.EventStore.Admin.Abstractions.Models.Projections;

namespace Hexalith.EventStore.Admin.Mcp;
/// <summary>
/// AdminApiClient partial — projection query methods.
/// </summary>
internal sealed partial class AdminApiClient {
    /// <summary>
    /// Lists all projections with their current status.
    /// </summary>
    public async Task<IReadOnlyList<ProjectionStatus>> ListProjectionsAsync(
        string? tenantId,
        CancellationToken cancellationToken) {
        string path = "/api/v1/admin/projections";
        if (!string.IsNullOrEmpty(tenantId)) {
            path += $"?tenantId={Uri.EscapeDataString(tenantId)}";
        }

        return await GetListAsync<ProjectionStatus>(path, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets detailed projection information including recent errors and configuration.
    /// </summary>
    public async Task<ProjectionDetail?> GetProjectionDetailAsync(
        string tenantId,
        string projectionName,
        CancellationToken cancellationToken) {
        string path = $"/api/v1/admin/projections/{Uri.EscapeDataString(tenantId)}/{Uri.EscapeDataString(projectionName)}";
        return await GetAsync<ProjectionDetail>(path, cancellationToken).ConfigureAwait(false);
    }
}
