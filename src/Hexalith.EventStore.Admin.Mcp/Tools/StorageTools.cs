namespace Hexalith.EventStore.Admin.Mcp.Tools;

using System.ComponentModel;

using ModelContextProtocol.Server;

/// <summary>
/// MCP tools for storage usage and metrics.
/// </summary>
[McpServerToolType]
internal static class StorageTools
{
    /// <summary>
    /// Get storage usage overview including event counts, sizes, and per-tenant breakdown.
    /// </summary>
    [McpServerTool(Name = "storage-overview")]
    [Description("Get storage usage overview including event counts, sizes, and per-tenant breakdown")]
    public static async Task<string> GetStorageOverview(
        AdminApiClient adminApiClient,
        [Description("Filter by tenant ID")] string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await adminApiClient.GetStorageOverviewAsync(tenantId, cancellationToken).ConfigureAwait(false);
            return result is null
                ? ToolHelper.SerializeError("not-found", "No storage overview data returned")
                : ToolHelper.SerializeResult(result);
        }
        catch (Exception ex)
        {
            return ToolHelper.HandleException(ex);
        }
    }
}
