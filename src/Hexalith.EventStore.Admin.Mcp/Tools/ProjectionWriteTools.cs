
using System.ComponentModel;

using Hexalith.EventStore.Admin.Abstractions.Models.Common;

using ModelContextProtocol.Server;

namespace Hexalith.EventStore.Admin.Mcp.Tools;
/// <summary>
/// MCP tools for approval-gated projection write operations.
/// </summary>
[McpServerToolType]
internal static class ProjectionWriteTools {
    /// <summary>
    /// Pause a running projection to stop event processing.
    /// </summary>
    [McpServerTool(Name = "projection-pause")]
    [Description("Pause a running projection to stop event processing (requires confirm: true)")]
    public static async Task<string> PauseProjection(
        AdminApiClient adminApiClient,
        [Description("Tenant ID")] string tenantId,
        [Description("Projection name")] string projectionName,
        [Description("Set to true to execute; false returns a preview")] bool confirm = false,
        CancellationToken cancellationToken = default) {
        string? validation = ToolHelper.ValidateRequired(
            (tenantId, "tenantId"), (projectionName, "projectionName"));
        if (validation is not null) {
            return validation;
        }

        if (!confirm) {
            return ToolHelper.SerializePreview(
                "projection-pause",
                $"Pause projection '{projectionName}' for tenant '{tenantId}'",
                $"POST /api/v1/admin/projections/{Uri.EscapeDataString(tenantId)}/{Uri.EscapeDataString(projectionName)}/pause",
                new { tenantId, projectionName },
                "This will stop the projection from processing new events until resumed.");
        }

        try {
            AdminOperationResult? result = await adminApiClient
                .PauseProjectionAsync(tenantId, projectionName, cancellationToken)
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
    /// Resume a paused projection to restart event processing.
    /// </summary>
    [McpServerTool(Name = "projection-resume")]
    [Description("Resume a paused projection to restart event processing (requires confirm: true)")]
    public static async Task<string> ResumeProjection(
        AdminApiClient adminApiClient,
        [Description("Tenant ID")] string tenantId,
        [Description("Projection name")] string projectionName,
        [Description("Set to true to execute; false returns a preview")] bool confirm = false,
        CancellationToken cancellationToken = default) {
        string? validation = ToolHelper.ValidateRequired(
            (tenantId, "tenantId"), (projectionName, "projectionName"));
        if (validation is not null) {
            return validation;
        }

        if (!confirm) {
            return ToolHelper.SerializePreview(
                "projection-resume",
                $"Resume projection '{projectionName}' for tenant '{tenantId}'",
                $"POST /api/v1/admin/projections/{Uri.EscapeDataString(tenantId)}/{Uri.EscapeDataString(projectionName)}/resume",
                new { tenantId, projectionName },
                "This will resume event processing for the projection.");
        }

        try {
            AdminOperationResult? result = await adminApiClient
                .ResumeProjectionAsync(tenantId, projectionName, cancellationToken)
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
    /// Reset a projection to rebuild state from a specific event position.
    /// </summary>
    [McpServerTool(Name = "projection-reset")]
    [Description("Reset a projection to rebuild state from a specific event position (requires confirm: true)")]
    public static async Task<string> ResetProjection(
        AdminApiClient adminApiClient,
        [Description("Tenant ID")] string tenantId,
        [Description("Projection name")] string projectionName,
        [Description("Event position to reset from (null = beginning)")] long? fromPosition = null,
        [Description("Set to true to execute; false returns a preview")] bool confirm = false,
        CancellationToken cancellationToken = default) {
        string? validation = ToolHelper.ValidateRequired(
            (tenantId, "tenantId"), (projectionName, "projectionName"));
        if (validation is not null) {
            return validation;
        }

        if (!confirm) {
            return ToolHelper.SerializePreview(
                "projection-reset",
                $"Reset projection '{projectionName}' for tenant '{tenantId}' from position {fromPosition?.ToString() ?? "beginning"}",
                $"POST /api/v1/admin/projections/{Uri.EscapeDataString(tenantId)}/{Uri.EscapeDataString(projectionName)}/reset",
                new { tenantId, projectionName, fromPosition },
                "This will clear projection state and rebuild from the specified position. This is a destructive operation.");
        }

        try {
            AdminOperationResult? result = await adminApiClient
                .ResetProjectionAsync(tenantId, projectionName, fromPosition, cancellationToken)
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
    /// Replay a projection between two event positions.
    /// </summary>
    [McpServerTool(Name = "projection-replay")]
    [Description("Replay a projection between two event positions (requires confirm: true)")]
    public static async Task<string> ReplayProjection(
        AdminApiClient adminApiClient,
        [Description("Tenant ID")] string tenantId,
        [Description("Projection name")] string projectionName,
        [Description("Start event position")] long fromPosition,
        [Description("End event position")] long toPosition,
        [Description("Set to true to execute; false returns a preview")] bool confirm = false,
        CancellationToken cancellationToken = default) {
        string? validation = ToolHelper.ValidateRequired(
            (tenantId, "tenantId"), (projectionName, "projectionName"));
        if (validation is not null) {
            return validation;
        }

        if (fromPosition > toPosition) {
            return ToolHelper.SerializeError("invalid-input", "fromPosition must be less than or equal to toPosition.");
        }

        if (!confirm) {
            return ToolHelper.SerializePreview(
                "projection-replay",
                $"Replay projection '{projectionName}' for tenant '{tenantId}' from position {fromPosition} to {toPosition}",
                $"POST /api/v1/admin/projections/{Uri.EscapeDataString(tenantId)}/{Uri.EscapeDataString(projectionName)}/replay",
                new { tenantId, projectionName, fromPosition, toPosition },
                "This will replay events between the specified positions. The projection will reprocess these events.");
        }

        try {
            AdminOperationResult? result = await adminApiClient
                .ReplayProjectionAsync(tenantId, projectionName, fromPosition, toPosition, cancellationToken)
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
