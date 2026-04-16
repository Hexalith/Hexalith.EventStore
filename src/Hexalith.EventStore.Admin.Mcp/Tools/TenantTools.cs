
using System.ComponentModel;

using Hexalith.EventStore.Admin.Abstractions.Models.Tenants;

using ModelContextProtocol.Server;

namespace Hexalith.EventStore.Admin.Mcp.Tools;
/// <summary>
/// MCP tools for querying tenant information.
/// </summary>
[McpServerToolType]
internal static class TenantTools {
    /// <summary>
    /// List all tenants with their status.
    /// </summary>
    [McpServerTool(Name = "tenant-list")]
    [Description("List all tenants with their name and status (Active/Disabled)")]
    public static async Task<string> ListTenants(
        AdminApiClient adminApiClient,
        CancellationToken cancellationToken = default) {
        try {
            IReadOnlyList<TenantSummary> result = await adminApiClient
                .ListTenantsAsync(cancellationToken)
                .ConfigureAwait(false);
            return ToolHelper.SerializeResult(result);
        }
        catch (Exception ex) {
            return ToolHelper.HandleException(ex);
        }
    }

    /// <summary>
    /// Get detailed tenant information including description, status, and creation date.
    /// </summary>
    [McpServerTool(Name = "tenant-detail")]
    [Description("Get detailed tenant information including description, status, and creation date")]
    public static async Task<string> GetTenantDetail(
        AdminApiClient adminApiClient,
        [Description("Tenant ID")] string tenantId,
        CancellationToken cancellationToken = default) {
        string? validation = ToolHelper.ValidateRequired((tenantId, "tenantId"));
        if (validation is not null) {
            return validation;
        }

        try {
            TenantDetail? result = await adminApiClient
                .GetTenantDetailAsync(tenantId, cancellationToken)
                .ConfigureAwait(false);
            return result is null
                ? ToolHelper.SerializeError("not-found", $"Tenant '{tenantId}' not found")
                : ToolHelper.SerializeResult(result);
        }
        catch (Exception ex) {
            return ToolHelper.HandleException(ex);
        }
    }

    /// <summary>
    /// List users assigned to a tenant with their roles.
    /// </summary>
    [McpServerTool(Name = "tenant-users")]
    [Description("List users assigned to a tenant with their user IDs and roles")]
    public static async Task<string> GetTenantUsers(
        AdminApiClient adminApiClient,
        [Description("Tenant ID")] string tenantId,
        CancellationToken cancellationToken = default) {
        string? validation = ToolHelper.ValidateRequired((tenantId, "tenantId"));
        if (validation is not null) {
            return validation;
        }

        try {
            IReadOnlyList<TenantUser> result = await adminApiClient
                .GetTenantUsersAsync(tenantId, cancellationToken)
                .ConfigureAwait(false);
            return ToolHelper.SerializeResult(result);
        }
        catch (Exception ex) {
            return ToolHelper.HandleException(ex);
        }
    }
}
