
using System.ComponentModel;

using Hexalith.EventStore.Admin.Abstractions.Models.Common;

using ModelContextProtocol.Server;

namespace Hexalith.EventStore.Admin.Mcp.Tools;
/// <summary>
/// MCP tools for approval-gated backup operations.
/// </summary>
[McpServerToolType]
internal static class BackupWriteTools {
    /// <summary>
    /// Trigger a full backup for a tenant.
    /// </summary>
    [McpServerTool(Name = "backup-trigger")]
    [Description("Trigger a full backup for a tenant (requires confirm: true)")]
    public static async Task<string> TriggerBackup(
        AdminApiClient adminApiClient,
        [Description("Tenant ID")] string tenantId,
        [Description("Optional backup description")] string? description = null,
        [Description("Include snapshots in backup")] bool includeSnapshots = true,
        [Description("Set to true to execute; false returns a preview")] bool confirm = false,
        CancellationToken cancellationToken = default) {
        string? validation = ToolHelper.ValidateRequired((tenantId, "tenantId"));
        if (validation is not null) {
            return validation;
        }

        if (!confirm) {
            return ToolHelper.SerializePreview(
                "backup-trigger",
                $"Trigger full backup for tenant '{tenantId}'" + (!string.IsNullOrWhiteSpace(description) ? $" ({description})" : string.Empty),
                $"POST /api/v1/admin/backups/{Uri.EscapeDataString(tenantId)}?includeSnapshots={includeSnapshots.ToString().ToLowerInvariant()}"
                    + (!string.IsNullOrWhiteSpace(description) ? $"&description={Uri.EscapeDataString(description)}" : string.Empty),
                new { tenantId, description, includeSnapshots },
                "This will initiate a full tenant backup. The operation runs asynchronously.");
        }

        try {
            AdminOperationResult? result = await adminApiClient
                .TriggerBackupAsync(tenantId, description, includeSnapshots, cancellationToken)
                .ConfigureAwait(false);
            return result is null
                ? ToolHelper.SerializeError("server-error", "No result returned from the server.")
                : ToolHelper.SerializeResult(result);
        }
        catch (Exception ex) {
            return ToolHelper.HandleException(ex);
        }
    }
}
