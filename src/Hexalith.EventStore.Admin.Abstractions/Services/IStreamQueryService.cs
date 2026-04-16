using Hexalith.EventStore.Admin.Abstractions.Models.Commands;
using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Abstractions.Models.Streams;

namespace Hexalith.EventStore.Admin.Abstractions.Services;

/// <summary>
/// Service interface for browsing and inspecting event streams.
/// </summary>
public interface IStreamQueryService {
    /// <summary>
    /// Gets recent commands across all streams, optionally filtered by tenant, status, and command type (FR69).
    /// </summary>
    /// <param name="tenantId">Optional tenant filter.</param>
    /// <param name="status">Optional status filter (string mapped to CommandStatus).</param>
    /// <param name="commandType">Optional command type filter (case-insensitive contains).</param>
    /// <param name="count">Maximum number of commands to return.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A paginated list of command summaries.</returns>
    Task<PagedResult<CommandSummary>> GetRecentCommandsAsync(string? tenantId, string? status, string? commandType, int count = 1000, CancellationToken ct = default);

    /// <summary>
    /// Gets recently active streams, optionally filtered by tenant and domain (FR68).
    /// </summary>
    /// <param name="tenantId">Optional tenant filter.</param>
    /// <param name="domain">Optional domain filter to scope investigation.</param>
    /// <param name="count">Maximum number of streams to return.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A paginated list of stream summaries.</returns>
    Task<PagedResult<StreamSummary>> GetRecentlyActiveStreamsAsync(string? tenantId, string? domain, int count = 1000, CancellationToken ct = default);

    /// <summary>
    /// Gets the timeline of commands, events, and queries for a specific stream (FR69).
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="domain">The domain name.</param>
    /// <param name="aggregateId">The aggregate identifier.</param>
    /// <param name="fromSequence">Optional starting sequence number.</param>
    /// <param name="toSequence">Optional ending sequence number.</param>
    /// <param name="count">Maximum entries per page.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A paginated list of timeline entries.</returns>
    Task<PagedResult<TimelineEntry>> GetStreamTimelineAsync(string tenantId, string domain, string aggregateId, long? fromSequence, long? toSequence, int count = 100, CancellationToken ct = default);

    /// <summary>
    /// Gets the aggregate state at a specific sequence position (FR70).
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="domain">The domain name.</param>
    /// <param name="aggregateId">The aggregate identifier.</param>
    /// <param name="sequenceNumber">The sequence number at which to reconstruct state.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The aggregate state snapshot.</returns>
    Task<AggregateStateSnapshot> GetAggregateStateAtPositionAsync(string tenantId, string domain, string aggregateId, long sequenceNumber, CancellationToken ct = default);

    /// <summary>
    /// Diffs aggregate state between two sequence positions (FR71).
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="domain">The domain name.</param>
    /// <param name="aggregateId">The aggregate identifier.</param>
    /// <param name="fromSequence">The starting sequence number.</param>
    /// <param name="toSequence">The ending sequence number.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The diff between the two state versions.</returns>
    Task<AggregateStateDiff> DiffAggregateStateAsync(string tenantId, string domain, string aggregateId, long fromSequence, long toSequence, CancellationToken ct = default);

    /// <summary>
    /// Gets detailed information about a single event for MCP diagnosis workflows (Journey 9).
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="domain">The domain name.</param>
    /// <param name="aggregateId">The aggregate identifier.</param>
    /// <param name="sequenceNumber">The sequence number of the event.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The event detail.</returns>
    Task<EventDetail> GetEventDetailAsync(string tenantId, string domain, string aggregateId, long sequenceNumber, CancellationToken ct = default);

    /// <summary>
    /// Gets the per-field blame (provenance) for an aggregate's state at a given sequence position (FR70+).
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="domain">The domain name.</param>
    /// <param name="aggregateId">The aggregate identifier.</param>
    /// <param name="atSequence">The sequence position to compute blame at. Null means latest.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The aggregate blame view with per-field provenance.</returns>
    Task<AggregateBlameView> GetAggregateBlameAsync(string tenantId, string domain, string aggregateId, long? atSequence, CancellationToken ct = default);

    /// <summary>
    /// Performs a binary search through event history to find the exact event where aggregate state
    /// diverged from expected field values.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="domain">The domain name.</param>
    /// <param name="aggregateId">The aggregate identifier.</param>
    /// <param name="goodSequence">The known-good sequence (state was correct here).</param>
    /// <param name="badSequence">The known-bad sequence (state diverged by this point).</param>
    /// <param name="fieldPaths">Optional field paths to watch. When null or empty, all leaf fields are compared.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The bisect result identifying the divergent event.</returns>
    Task<BisectResult> BisectAsync(string tenantId, string domain, string aggregateId, long goodSequence, long badSequence, IReadOnlyList<string>? fieldPaths, CancellationToken ct = default);

    /// <summary>
    /// Gets a single step-through debugging frame combining event metadata, aggregate state,
    /// and field changes at the specified sequence position.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="domain">The domain name.</param>
    /// <param name="aggregateId">The aggregate identifier.</param>
    /// <param name="sequenceNumber">The sequence number (1-based) to retrieve.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The event step frame at the specified position.</returns>
    Task<EventStepFrame> GetEventStepFrameAsync(string tenantId, string domain, string aggregateId, long sequenceNumber, CancellationToken ct = default);

    /// <summary>
    /// Executes a command in sandbox (dry-run) mode against reconstructed aggregate state.
    /// Invokes the domain service Handle method but does NOT persist any events.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="domain">The domain name.</param>
    /// <param name="aggregateId">The aggregate identifier.</param>
    /// <param name="request">The sandbox command request containing command type, payload, and target sequence.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The sandbox execution result with produced events, resulting state, and state diff.</returns>
    Task<SandboxResult?> SandboxCommandAsync(string tenantId, string domain, string aggregateId, SandboxCommandRequest request, CancellationToken ct = default);

    /// <summary>
    /// Traces the causation chain starting from a specific event (FR72).
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="domain">The domain name.</param>
    /// <param name="aggregateId">The aggregate identifier.</param>
    /// <param name="sequenceNumber">The sequence number of the event to trace from.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The causation chain.</returns>
    Task<CausationChain> TraceCausationChainAsync(string tenantId, string domain, string aggregateId, long sequenceNumber, CancellationToken ct = default);

    /// <summary>
    /// Gets the correlation trace map for a given correlation ID, showing the complete command lifecycle (FR72).
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="correlationId">The correlation ID to trace.</param>
    /// <param name="domain">Optional domain hint for events-first scanning when command status has expired.</param>
    /// <param name="aggregateId">Optional aggregate ID hint for events-first scanning when command status has expired.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The correlation trace map.</returns>
    Task<CorrelationTraceMap> GetCorrelationTraceMapAsync(string tenantId, string correlationId, string? domain, string? aggregateId, CancellationToken ct = default);
}
