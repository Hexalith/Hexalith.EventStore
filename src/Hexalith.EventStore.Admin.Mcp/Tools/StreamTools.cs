
using System.ComponentModel;

using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Abstractions.Models.Streams;

using ModelContextProtocol.Server;

namespace Hexalith.EventStore.Admin.Mcp.Tools;
/// <summary>
/// MCP tools for querying event streams.
/// </summary>
[McpServerToolType]
internal static class StreamTools {
    /// <summary>
    /// List recently active event streams, optionally filtered by tenant and domain.
    /// </summary>
    [McpServerTool(Name = "stream-list")]
    [Description("List recently active event streams, optionally filtered by tenant and domain")]
    public static async Task<string> ListStreams(
        AdminApiClient adminApiClient,
        InvestigationSession session,
        [Description("Filter by tenant ID (uses session context if omitted)")] string? tenantId = null,
        [Description("Filter by domain (uses session context if omitted)")] string? domain = null,
        [Description("Max streams to return (default 100)")] int count = 100,
        CancellationToken cancellationToken = default) {
        tenantId = NormalizeOptionalScope(tenantId);
        domain = NormalizeOptionalScope(domain);

        InvestigationSession.Snapshot snapshot = session.GetSnapshot();
        tenantId ??= snapshot.TenantId;
        domain ??= snapshot.Domain;

        try {
            PagedResult<StreamSummary>? result = await adminApiClient.GetRecentlyActiveStreamsAsync(tenantId, domain, count, cancellationToken).ConfigureAwait(false);
            return result is null
                ? ToolHelper.SerializeError("not-found", "No stream data returned")
                : ToolHelper.SerializeResult(result);
        }
        catch (Exception ex) {
            return ToolHelper.HandleException(ex);
        }
    }

    /// <summary>
    /// Get the command/event/query timeline for a specific event stream.
    /// </summary>
    [McpServerTool(Name = "stream-events")]
    [Description("Get the command/event/query timeline for a specific event stream")]
    public static async Task<string> GetStreamEvents(
        AdminApiClient adminApiClient,
        InvestigationSession session,
        [Description("Tenant ID")] string tenantId,
        [Description("Domain name")] string domain,
        [Description("Aggregate ID")] string aggregateId,
        [Description("Start from sequence number")] long? fromSequence = null,
        [Description("End at sequence number")] long? toSequence = null,
        [Description("Max entries to return (default 100)")] int count = 100,
        CancellationToken cancellationToken = default) {
        string? validation = ToolHelper.ValidateRequired((tenantId, "tenantId"), (domain, "domain"), (aggregateId, "aggregateId"));
        if (validation is not null) {
            return validation;
        }

        try {
            PagedResult<TimelineEntry>? result = await adminApiClient.GetStreamTimelineAsync(tenantId, domain, aggregateId, fromSequence, toSequence, count, cancellationToken).ConfigureAwait(false);
            return result is null
                ? ToolHelper.SerializeError("not-found", $"No timeline found for {domain}/{aggregateId}")
                : ToolHelper.SerializeResult(result);
        }
        catch (Exception ex) {
            return ToolHelper.HandleException(ex);
        }
    }

    /// <summary>
    /// Get the aggregate state reconstructed at a specific sequence number (point-in-time state exploration).
    /// </summary>
    [McpServerTool(Name = "stream-state")]
    [Description("Get the aggregate state reconstructed at a specific sequence number (point-in-time state exploration)")]
    public static async Task<string> GetStreamState(
        AdminApiClient adminApiClient,
        InvestigationSession session,
        [Description("Tenant ID")] string tenantId,
        [Description("Domain name")] string domain,
        [Description("Aggregate ID")] string aggregateId,
        [Description("Sequence number to reconstruct state at")] long sequenceNumber,
        CancellationToken cancellationToken = default) {
        string? validation = ToolHelper.ValidateRequired((tenantId, "tenantId"), (domain, "domain"), (aggregateId, "aggregateId"));
        if (validation is not null) {
            return validation;
        }

        try {
            AggregateStateSnapshot? result = await adminApiClient.GetAggregateStateAsync(tenantId, domain, aggregateId, sequenceNumber, cancellationToken).ConfigureAwait(false);
            return result is null
                ? ToolHelper.SerializeError("not-found", $"No aggregate state found for {domain}/{aggregateId} at sequence {sequenceNumber}")
                : ToolHelper.SerializeResult(result);
        }
        catch (Exception ex) {
            return ToolHelper.HandleException(ex);
        }
    }

    /// <summary>
    /// Get full details of a specific event including its payload and metadata.
    /// </summary>
    [McpServerTool(Name = "stream-event-detail")]
    [Description("Get full details of a specific event including its payload and metadata")]
    public static async Task<string> GetEventDetail(
        AdminApiClient adminApiClient,
        InvestigationSession session,
        [Description("Tenant ID")] string tenantId,
        [Description("Domain name")] string domain,
        [Description("Aggregate ID")] string aggregateId,
        [Description("Event sequence number")] long sequenceNumber,
        CancellationToken cancellationToken = default) {
        string? validation = ToolHelper.ValidateRequired((tenantId, "tenantId"), (domain, "domain"), (aggregateId, "aggregateId"));
        if (validation is not null) {
            return validation;
        }

        try {
            EventDetail? result = await adminApiClient.GetEventDetailAsync(tenantId, domain, aggregateId, sequenceNumber, cancellationToken).ConfigureAwait(false);
            return result is null
                ? ToolHelper.SerializeError("not-found", $"No event found for {domain}/{aggregateId} at sequence {sequenceNumber}")
                : ToolHelper.SerializeResult(result);
        }
        catch (Exception ex) {
            return ToolHelper.HandleException(ex);
        }
    }

    private static string? NormalizeOptionalScope(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
