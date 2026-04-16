
using System.ComponentModel;

using Hexalith.EventStore.Admin.Abstractions.Models.Common;

using ModelContextProtocol.Server;

namespace Hexalith.EventStore.Admin.Mcp.Tools;
/// <summary>
/// MCP tools for approval-gated consistency check operations.
/// </summary>
[McpServerToolType]
internal static class ConsistencyWriteTools {
    /// <summary>
    /// Trigger a data integrity check across streams and projections.
    /// </summary>
    [McpServerTool(Name = "consistency-trigger")]
    [Description("Trigger a data integrity check across streams and projections (requires confirm: true)")]
    public static async Task<string> TriggerCheck(
        AdminApiClient adminApiClient,
        [Description("Comma-separated check types: SequenceContinuity, SnapshotIntegrity, ProjectionPositions, MetadataConsistency")] string checkTypes,
        [Description("Filter by tenant ID")] string? tenantId = null,
        [Description("Filter by domain")] string? domain = null,
        [Description("Set to true to execute; false returns a preview")] bool confirm = false,
        CancellationToken cancellationToken = default) {
        string? validation = ToolHelper.ValidateRequired((checkTypes, "checkTypes"));
        if (validation is not null) {
            return validation;
        }

        string[] parsedTypes = checkTypes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parsedTypes.Length == 0) {
            return ToolHelper.SerializeError(
                "invalid-input",
                "At least one check type is required. Valid types: SequenceContinuity, SnapshotIntegrity, ProjectionPositions, MetadataConsistency");
        }

        if (!confirm) {
            return ToolHelper.SerializePreview(
                "consistency-trigger",
                $"Trigger consistency check ({string.Join(", ", parsedTypes)})"
                    + (tenantId is not null ? $" for tenant '{tenantId}'" : string.Empty)
                    + (domain is not null ? $" in domain '{domain}'" : string.Empty),
                "POST /api/v1/admin/consistency/checks",
                new { tenantId, domain, checkTypes = parsedTypes },
                "This will trigger a data integrity check. Checks run asynchronously and may take time depending on data volume.");
        }

        try {
            AdminOperationResult? result = await adminApiClient
                .TriggerConsistencyCheckAsync(tenantId, domain, parsedTypes, cancellationToken)
                .ConfigureAwait(false);
            return result is null
                ? ToolHelper.SerializeError("server-error", "No result returned from the server.")
                : ToolHelper.SerializeResult(result);
        }
        catch (Exception ex) {
            return ToolHelper.HandleException(ex);
        }
    }

    /// <summary>
    /// Cancel a running consistency check.
    /// </summary>
    [McpServerTool(Name = "consistency-cancel")]
    [Description("Cancel a running consistency check (requires confirm: true)")]
    public static async Task<string> CancelCheck(
        AdminApiClient adminApiClient,
        [Description("Consistency check ID")] string checkId,
        [Description("Set to true to execute; false returns a preview")] bool confirm = false,
        CancellationToken cancellationToken = default) {
        string? validation = ToolHelper.ValidateRequired((checkId, "checkId"));
        if (validation is not null) {
            return validation;
        }

        if (!confirm) {
            return ToolHelper.SerializePreview(
                "consistency-cancel",
                $"Cancel consistency check '{checkId}'",
                $"POST /api/v1/admin/consistency/checks/{Uri.EscapeDataString(checkId)}/cancel",
                new { checkId },
                "This will cancel the running consistency check. Partial results will be preserved.");
        }

        try {
            AdminOperationResult? result = await adminApiClient
                .CancelConsistencyCheckAsync(checkId, cancellationToken)
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
