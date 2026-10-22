
using System.ComponentModel;

using Hexalith.EventStore.Admin.Abstractions.Models.Consistency;

using ModelContextProtocol.Server;

namespace Hexalith.EventStore.Admin.Mcp.Tools;
/// <summary>
/// MCP tools for querying data integrity consistency checks.
/// </summary>
[McpServerToolType]
internal static class ConsistencyTools {
    /// <summary>
    /// List data integrity checks with status, scope, and anomaly counts.
    /// </summary>
    [McpServerTool(Name = "consistency-list")]
    [Description("List data integrity checks with status, scope, and anomaly counts")]
    public static async Task<string> ListChecks(
        AdminApiClient adminApiClient,
        InvestigationSession session,
        [Description("Filter by tenant ID (uses session context if omitted)")] string? tenantId = null,
        CancellationToken cancellationToken = default) {
        tenantId = NormalizeOptionalScope(tenantId);
        tenantId ??= session.GetSnapshot().TenantId;

        try {
            IReadOnlyList<ConsistencyCheckSummary> result = await adminApiClient.GetConsistencyChecksAsync(tenantId, cancellationToken).ConfigureAwait(false);
            return ToolHelper.SerializeResult(result);
        }
        catch (Exception ex) {
            return ToolHelper.HandleException(ex);
        }
    }

    /// <summary>
    /// Get detailed data integrity check results including anomalies, severity levels, and affected streams.
    /// </summary>
    [McpServerTool(Name = "consistency-detail")]
    [Description("Get detailed data integrity check results including anomalies, severity levels, and affected streams")]
    public static async Task<string> GetCheckDetail(
        AdminApiClient adminApiClient,
        InvestigationSession session,
        [Description("Consistency check ID")] string checkId,
        CancellationToken cancellationToken = default) {
        string? validation = ToolHelper.ValidateRequired((checkId, "checkId"));
        if (validation is not null) {
            return validation;
        }

        try {
            ConsistencyCheckResult? result = await adminApiClient.GetConsistencyCheckResultAsync(checkId, cancellationToken).ConfigureAwait(false);
            return result is null
                ? ToolHelper.SerializeError("not-found", $"No consistency check found with ID '{checkId}'")
                : ToolHelper.SerializeResult(result);
        }
        catch (Exception ex) {
            return ToolHelper.HandleException(ex);
        }
    }

    private static string? NormalizeOptionalScope(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
