using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Abstractions.Models.Streams;

namespace Hexalith.EventStore.Admin.Abstractions.Services;

/// <summary>
/// Service interface for browsing and inspecting event streams.
/// </summary>
public interface IStreamQueryService
{
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
    /// Traces the causation chain starting from a specific event (FR72).
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="domain">The domain name.</param>
    /// <param name="aggregateId">The aggregate identifier.</param>
    /// <param name="sequenceNumber">The sequence number of the event to trace from.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The causation chain.</returns>
    Task<CausationChain> TraceCausationChainAsync(string tenantId, string domain, string aggregateId, long sequenceNumber, CancellationToken ct = default);
}
