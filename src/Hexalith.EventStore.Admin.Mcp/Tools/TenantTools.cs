namespace Hexalith.EventStore.Admin.Mcp.Tools;

using System.ComponentModel;

using ModelContextProtocol.Server;

/// <summary>
/// MCP tools for querying tenant information.
/// </summary>
[McpServerToolType]
internal static class TenantTools
{
    /// <summary>
    /// List all tenants with status, event counts, and domain counts.
    /// </summary>
    [McpServerTool(Name = "tenant-list")]
    [Description("List all tenants with status, event counts, and domain counts")]
    public static async Task<string> ListTenants(
        AdminApiClient adminApiClient,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await adminApiClient
                .ListTenantsAsync(cancellationToken)
                .ConfigureAwait(false);
            return ToolHelper.SerializeResult(result);
        }
        catch (Exception ex)
        {
            return ToolHelper.HandleException(ex);
        }
    }

    /// <summary>
    /// Get detailed tenant information including quotas, storage, and subscription tier.
    /// </summary>
    [McpServerTool(Name = "tenant-detail")]
    [Description("Get detailed tenant information including quotas, storage, and subscription tier")]
    public static async Task<string> GetTenantDetail(
        AdminApiClient adminApiClient,
        [Description("Tenant ID")] string tenantId,
        CancellationToken cancellationToken = default)
    {
        string? validation = ToolHelper.ValidateRequired((tenantId, "tenantId"));
        if (validation is not null)
        {
            return validation;
        }

        try
        {
            var result = await adminApiClient
                .GetTenantDetailAsync(tenantId, cancellationToken)
                .ConfigureAwait(false);
            return result is null
                ? ToolHelper.SerializeError("not-found", $"Tenant '{tenantId}' not found")
                : ToolHelper.SerializeResult(result);
        }
        catch (Exception ex)
        {
            return ToolHelper.HandleException(ex);
        }
    }

    /// <summary>
    /// Get tenant quota limits and current usage.
    /// </summary>
    [McpServerTool(Name = "tenant-quotas")]
    [Description("Get tenant quota limits and current usage")]
    public static async Task<string> GetTenantQuotas(
        AdminApiClient adminApiClient,
        [Description("Tenant ID")] string tenantId,
        CancellationToken cancellationToken = default)
    {
        string? validation = ToolHelper.ValidateRequired((tenantId, "tenantId"));
        if (validation is not null)
        {
            return validation;
        }

        try
        {
            var result = await adminApiClient
                .GetTenantQuotasAsync(tenantId, cancellationToken)
                .ConfigureAwait(false);
            return result is null
                ? ToolHelper.SerializeError("not-found", $"Tenant '{tenantId}' not found")
                : ToolHelper.SerializeResult(result);
        }
        catch (Exception ex)
        {
            return ToolHelper.HandleException(ex);
        }
    }

    /// <summary>
    /// List users assigned to a tenant with their roles.
    /// </summary>
    [McpServerTool(Name = "tenant-users")]
    [Description("List users assigned to a tenant with their roles")]
    public static async Task<string> GetTenantUsers(
        AdminApiClient adminApiClient,
        [Description("Tenant ID")] string tenantId,
        CancellationToken cancellationToken = default)
    {
        string? validation = ToolHelper.ValidateRequired((tenantId, "tenantId"));
        if (validation is not null)
        {
            return validation;
        }

        try
        {
            var result = await adminApiClient
                .GetTenantUsersAsync(tenantId, cancellationToken)
                .ConfigureAwait(false);
            return ToolHelper.SerializeResult(result);
        }
        catch (Exception ex)
        {
            return ToolHelper.HandleException(ex);
        }
    }
}
