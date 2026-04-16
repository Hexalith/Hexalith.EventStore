
using System.ComponentModel;

using Hexalith.EventStore.Admin.Abstractions.Models.Storage;

using ModelContextProtocol.Server;

namespace Hexalith.EventStore.Admin.Mcp.Tools;
/// <summary>
/// MCP tools for storage usage and metrics.
/// </summary>
[McpServerToolType]
internal static class StorageTools {
    /// <summary>
    /// Get storage usage overview including event counts, sizes, and per-tenant breakdown.
    /// </summary>
    [McpServerTool(Name = "storage-overview")]
    [Description("Get storage usage overview including event counts, sizes, and per-tenant breakdown")]
    public static async Task<string> GetStorageOverview(
        AdminApiClient adminApiClient,
        InvestigationSession session,
        [Description("Filter by tenant ID (uses session context if omitted)")] string? tenantId = null,
        CancellationToken cancellationToken = default) {
        tenantId = NormalizeOptionalScope(tenantId);
        tenantId ??= session.GetSnapshot().TenantId;

        try {
            StorageOverview? result = await adminApiClient.GetStorageOverviewAsync(tenantId, cancellationToken).ConfigureAwait(false);
            return result is null
                ? ToolHelper.SerializeError("not-found", "No storage overview data returned")
                : ToolHelper.SerializeResult(result);
        }
        catch (Exception ex) {
            return ToolHelper.HandleException(ex);
        }
    }

    private static string? NormalizeOptionalScope(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
