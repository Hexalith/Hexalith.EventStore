namespace Hexalith.EventStore.Admin.Mcp.Tools;

using System.ComponentModel;

using ModelContextProtocol.Server;

/// <summary>
/// MCP tools for querying projections.
/// </summary>
[McpServerToolType]
internal static class ProjectionTools
{
    /// <summary>
    /// List all projections with their current status, lag, and error counts.
    /// </summary>
    [McpServerTool(Name = "projection-list")]
    [Description("List all projections with their current status, lag, and error counts")]
    public static async Task<string> ListProjections(
        AdminApiClient adminApiClient,
        [Description("Filter by tenant ID")] string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await adminApiClient.ListProjectionsAsync(tenantId, cancellationToken).ConfigureAwait(false);
            return ToolHelper.SerializeResult(result);
        }
        catch (Exception ex)
        {
            return ToolHelper.HandleException(ex);
        }
    }

    /// <summary>
    /// Get detailed projection information including recent errors and configuration.
    /// </summary>
    [McpServerTool(Name = "projection-detail")]
    [Description("Get detailed projection information including recent errors and configuration")]
    public static async Task<string> GetProjectionDetail(
        AdminApiClient adminApiClient,
        [Description("Tenant ID")] string tenantId,
        [Description("Projection name")] string projectionName,
        CancellationToken cancellationToken = default)
    {
        string? validation = ToolHelper.ValidateRequired((tenantId, "tenantId"), (projectionName, "projectionName"));
        if (validation is not null)
        {
            return validation;
        }

        try
        {
            var result = await adminApiClient.GetProjectionDetailAsync(tenantId, projectionName, cancellationToken).ConfigureAwait(false);
            return result is null
                ? ToolHelper.SerializeError("not-found", $"Projection '{projectionName}' not found for tenant '{tenantId}'")
                : ToolHelper.SerializeResult(result);
        }
        catch (Exception ex)
        {
            return ToolHelper.HandleException(ex);
        }
    }
}
