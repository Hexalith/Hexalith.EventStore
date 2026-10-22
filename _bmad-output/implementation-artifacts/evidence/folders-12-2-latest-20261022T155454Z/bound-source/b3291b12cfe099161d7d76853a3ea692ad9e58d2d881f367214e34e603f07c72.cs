
using System.ComponentModel;

using Hexalith.EventStore.Admin.Abstractions.Models.Streams;

using ModelContextProtocol.Server;

namespace Hexalith.EventStore.Admin.Mcp.Tools;
/// <summary>
/// MCP tools for stream-level diagnostics: state diff and causation chain tracing.
/// </summary>
[McpServerToolType]
internal static class DiagnosticTools {
    /// <summary>
    /// Diff aggregate state between two sequence positions, showing which fields changed and their before/after values.
    /// </summary>
    [McpServerTool(Name = "stream-diff")]
    [Description("Diff aggregate state between two sequence positions, showing which fields changed and their before/after values")]
    public static async Task<string> DiffAggregateState(
        AdminApiClient adminApiClient,
        [Description("Tenant ID")] string tenantId,
        [Description("Domain name")] string domain,
        [Description("Aggregate ID")] string aggregateId,
        [Description("Start sequence number")] long fromSequence,
        [Description("End sequence number")] long toSequence,
        CancellationToken cancellationToken = default) {
        string? validation = ToolHelper.ValidateRequired((tenantId, "tenantId"), (domain, "domain"), (aggregateId, "aggregateId"));
        if (validation is not null) {
            return validation;
        }

        try {
            AggregateStateDiff? result = await adminApiClient.DiffAggregateStateAsync(tenantId, domain, aggregateId, fromSequence, toSequence, cancellationToken).ConfigureAwait(false);
            return result is null
                ? ToolHelper.SerializeError("not-found", $"No diff found for {domain}/{aggregateId} between sequences {fromSequence} and {toSequence}")
                : ToolHelper.SerializeResult(result);
        }
        catch (Exception ex) {
            return ToolHelper.HandleException(ex);
        }
    }

    /// <summary>
    /// Trace the full causation chain for an event — originating command, all events produced, and affected projections.
    /// </summary>
    [McpServerTool(Name = "causation-chain")]
    [Description("Trace the full causation chain for an event — originating command, all events produced, and affected projections")]
    public static async Task<string> TraceCausationChain(
        AdminApiClient adminApiClient,
        [Description("Tenant ID")] string tenantId,
        [Description("Domain name")] string domain,
        [Description("Aggregate ID")] string aggregateId,
        [Description("Event sequence number to trace from")] long sequenceNumber,
        CancellationToken cancellationToken = default) {
        string? validation = ToolHelper.ValidateRequired((tenantId, "tenantId"), (domain, "domain"), (aggregateId, "aggregateId"));
        if (validation is not null) {
            return validation;
        }

        try {
            CausationChain? result = await adminApiClient.TraceCausationChainAsync(tenantId, domain, aggregateId, sequenceNumber, cancellationToken).ConfigureAwait(false);
            return result is null
                ? ToolHelper.SerializeError("not-found", $"No causation chain found for {domain}/{aggregateId} at sequence {sequenceNumber}")
                : ToolHelper.SerializeResult(result);
        }
        catch (Exception ex) {
            return ToolHelper.HandleException(ex);
        }
    }
}
