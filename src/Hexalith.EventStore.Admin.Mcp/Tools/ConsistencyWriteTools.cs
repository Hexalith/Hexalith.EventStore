
using System.ComponentModel;

using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Abstractions.Models.Consistency;

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
        [Description("Tenant ID")] string tenantId,
        [Description("Filter by domain")] string? domain = null,
        [Description("Set to true to execute; false returns a preview")] bool confirm = false,
        CancellationToken cancellationToken = default) {
        domain = string.IsNullOrWhiteSpace(domain) ? null : domain.Trim();
        string? validation = ToolHelper.ValidateRequired((checkTypes, "checkTypes"), (tenantId, "tenantId"))
            ?? ToolHelper.ValidateTenantId(tenantId)
            ?? ToolHelper.ValidatePreviewMatchesExecution(
                (checkTypes, "checkTypes"),
                (tenantId, "tenantId"),
                (domain, "domain"));
        if (validation is not null) {
            return validation;
        }

        string[] requestedTypes = checkTypes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (requestedTypes.Length == 0) {
            return ToolHelper.SerializeError(
                "invalid-input",
                "At least one check type is required. Valid types: SequenceContinuity, SnapshotIntegrity, ProjectionPositions, MetadataConsistency");
        }

        var parsedTypes = new List<ConsistencyCheckType>(requestedTypes.Length);
        HashSet<ConsistencyCheckType> seenTypes = [];
        foreach (string requestedType in requestedTypes) {
            if (!Enum.TryParse(requestedType, ignoreCase: true, out ConsistencyCheckType parsedType)
                || !Enum.IsDefined(parsedType)
                || !string.Equals(Enum.GetName(parsedType), requestedType, StringComparison.OrdinalIgnoreCase)) {
                return ToolHelper.SerializeError(
                    "invalid-input",
                    $"Unknown check type '{requestedType}'. Valid types: SequenceContinuity, SnapshotIntegrity, ProjectionPositions, MetadataConsistency");
            }

            if (seenTypes.Add(parsedType)) {
                parsedTypes.Add(parsedType);
            }
        }

        string[] previewTypes = parsedTypes.Select(checkType => checkType.ToString()).ToArray();
        string target = $"Trigger consistency check ({string.Join(", ", previewTypes)}) for tenant '{tenantId}'"
            + (domain is not null ? $" in domain '{domain}'" : string.Empty);
        const string endpoint = "POST /api/v1/admin/consistency/checks";
        validation = ToolHelper.ValidatePreviewMatchesExecution((target, "target"), (endpoint, "endpoint"));
        if (validation is not null) {
            return validation;
        }

        if (!confirm) {
            return ToolHelper.SerializePreview(
                "consistency-trigger",
                target,
                endpoint,
                new { tenantId, domain, checkTypes = previewTypes },
                "This will trigger a data integrity check. Checks run asynchronously and may take time depending on data volume.",
                "Operator");
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
        string? validation = ToolHelper.ValidateRequired((checkId, "checkId"))
            ?? ToolHelper.ValidatePathSegments((checkId, "checkId"))
            ?? ToolHelper.ValidatePreviewMatchesExecution((checkId, "checkId"));
        if (validation is not null) {
            return validation;
        }

        string target = $"Cancel consistency check '{checkId}'";
        string endpoint = $"POST /api/v1/admin/consistency/checks/{Uri.EscapeDataString(checkId)}/cancel";
        validation = ToolHelper.ValidatePreviewMatchesExecution((target, "target"), (endpoint, "endpoint"));
        if (validation is not null) {
            return validation;
        }

        if (!confirm) {
            return ToolHelper.SerializePreview(
                "consistency-cancel",
                target,
                endpoint,
                new { checkId },
                "This will cancel the running consistency check. Partial results will be preserved.",
                "Admin");
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
