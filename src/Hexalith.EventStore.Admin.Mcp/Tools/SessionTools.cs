
using System.ComponentModel;

using ModelContextProtocol.Server;

namespace Hexalith.EventStore.Admin.Mcp.Tools;
/// <summary>
/// MCP tools for managing investigation session context.
/// </summary>
[McpServerToolType]
internal static class SessionTools {
    /// <summary>
    /// Set or clear tenant and/or domain scope for subsequent queries.
    /// </summary>
    [McpServerTool(Name = "session-set-context")]
    [Description("Set or clear tenant and/or domain scope for subsequent queries — reduces parameter repetition and prevents cross-tenant leakage")]
    public static Task<string> SetContext(
        InvestigationSession session,
        [Description("Tenant ID to scope queries to")] string? tenantId = null,
        [Description("Domain name to scope queries to")] string? domain = null,
        [Description("Set to true to clear the tenant scope")] bool clearTenantId = false,
        [Description("Set to true to clear the domain scope")] bool clearDomain = false) {
        tenantId = NormalizeScopeInput(tenantId);
        domain = NormalizeScopeInput(domain);

        // Validate: can't set and clear the same field
        if (tenantId is not null && clearTenantId) {
            return Task.FromResult(
                ToolHelper.SerializeError("invalid-input", "Cannot set 'tenantId' and 'clearTenantId' simultaneously."));
        }

        if (domain is not null && clearDomain) {
            return Task.FromResult(
                ToolHelper.SerializeError("invalid-input", "Cannot set 'domain' and 'clearDomain' simultaneously."));
        }

        // Validate: at least one action
        if (tenantId is null && domain is null && !clearTenantId && !clearDomain) {
            return Task.FromResult(
                ToolHelper.SerializeError("invalid-input", "At least one of 'tenantId', 'domain', 'clearTenantId', or 'clearDomain' must be provided."));
        }

        // Capture previous state for change tracking
        InvestigationSession.Snapshot previous = session.GetSnapshot();

        // Apply clear operations first
        if (clearTenantId) {
            session.ClearTenantId();
        }

        if (clearDomain) {
            session.ClearDomain();
        }

        // Apply set operations
        if (tenantId is not null || domain is not null) {
            session.SetContext(tenantId, domain);
        }

        InvestigationSession.Snapshot current = session.GetSnapshot();

        return Task.FromResult(ToolHelper.SerializeResult(new {
            contextSet = true,
            tenantId = current.TenantId,
            domain = current.Domain,
            previousTenantId = previous.TenantId,
            previousDomain = previous.Domain,
            startedAtUtc = current.StartedAtUtc,
        }));
    }

    /// <summary>
    /// Get the current investigation session context (active tenant and domain scope).
    /// </summary>
    [McpServerTool(Name = "session-get-context")]
    [Description("Get the current investigation session context (active tenant and domain scope)")]
    public static Task<string> GetContext(
        InvestigationSession session) {
        InvestigationSession.Snapshot snapshot = session.GetSnapshot();

        return Task.FromResult(ToolHelper.SerializeResult(new {
            tenantId = snapshot.TenantId,
            domain = snapshot.Domain,
            startedAtUtc = snapshot.StartedAtUtc,
            hasContext = snapshot.HasContext,
        }));
    }

    /// <summary>
    /// Clear the investigation session context to remove tenant and domain scope.
    /// </summary>
    [McpServerTool(Name = "session-clear-context")]
    [Description("Clear the investigation session context to remove tenant and domain scope")]
    public static Task<string> ClearContext(
        InvestigationSession session) {
        session.Clear();

        return Task.FromResult(ToolHelper.SerializeResult(new {
            contextCleared = true,
            tenantId = (string?)null,
            domain = (string?)null,
            startedAtUtc = (DateTimeOffset?)null,
            hasContext = false,
            note = "Server-side context cleared. Prior query results in your conversation history may still contain data from the previous tenant scope.",
        }));
    }

    private static string? NormalizeScopeInput(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
