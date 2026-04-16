
using Hexalith.EventStore.Admin.Abstractions.Models.Consistency;

namespace Hexalith.EventStore.Admin.Mcp;
/// <summary>
/// AdminApiClient partial — consistency check query methods.
/// </summary>
internal sealed partial class AdminApiClient {
    /// <summary>
    /// Lists consistency check summaries, optionally filtered by tenant.
    /// </summary>
    public async Task<IReadOnlyList<ConsistencyCheckSummary>> GetConsistencyChecksAsync(
        string? tenantId,
        CancellationToken cancellationToken) {
        string path = "/api/v1/admin/consistency/checks";
        if (!string.IsNullOrWhiteSpace(tenantId)) {
            path += $"?tenantId={Uri.EscapeDataString(tenantId)}";
        }

        return await GetListAsync<ConsistencyCheckSummary>(path, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the full result of a consistency check including anomaly details.
    /// </summary>
    public async Task<ConsistencyCheckResult?> GetConsistencyCheckResultAsync(
        string checkId,
        CancellationToken cancellationToken) {
        string path = $"/api/v1/admin/consistency/checks/{Uri.EscapeDataString(checkId)}";
        return await GetAsync<ConsistencyCheckResult>(path, cancellationToken).ConfigureAwait(false);
    }
}
