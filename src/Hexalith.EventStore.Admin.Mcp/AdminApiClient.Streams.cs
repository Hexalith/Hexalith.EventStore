namespace Hexalith.EventStore.Admin.Mcp;

using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Abstractions.Models.Streams;

/// <summary>
/// AdminApiClient partial — stream query methods.
/// </summary>
internal sealed partial class AdminApiClient
{
    /// <summary>
    /// Lists recently active event streams with optional filtering.
    /// </summary>
    public async Task<PagedResult<StreamSummary>?> GetRecentlyActiveStreamsAsync(
        string? tenantId,
        string? domain,
        int count,
        CancellationToken cancellationToken)
    {
        string path = $"/api/v1/admin/streams?count={count}";
        if (!string.IsNullOrEmpty(tenantId))
        {
            path += $"&tenantId={Uri.EscapeDataString(tenantId)}";
        }

        if (!string.IsNullOrEmpty(domain))
        {
            path += $"&domain={Uri.EscapeDataString(domain)}";
        }

        return await GetAsync<PagedResult<StreamSummary>>(path, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the command/event/query timeline for a specific event stream.
    /// </summary>
    public async Task<PagedResult<TimelineEntry>?> GetStreamTimelineAsync(
        string tenantId,
        string domain,
        string aggregateId,
        long? fromSequence,
        long? toSequence,
        int count,
        CancellationToken cancellationToken)
    {
        string path = $"/api/v1/admin/streams/{Uri.EscapeDataString(tenantId)}/{Uri.EscapeDataString(domain)}/{Uri.EscapeDataString(aggregateId)}/timeline?count={count}";
        if (fromSequence.HasValue)
        {
            path += $"&fromSequence={fromSequence.Value}";
        }

        if (toSequence.HasValue)
        {
            path += $"&toSequence={toSequence.Value}";
        }

        return await GetAsync<PagedResult<TimelineEntry>>(path, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the aggregate state reconstructed at a specific sequence number.
    /// </summary>
    public async Task<AggregateStateSnapshot?> GetAggregateStateAsync(
        string tenantId,
        string domain,
        string aggregateId,
        long sequenceNumber,
        CancellationToken cancellationToken)
    {
        string path = $"/api/v1/admin/streams/{Uri.EscapeDataString(tenantId)}/{Uri.EscapeDataString(domain)}/{Uri.EscapeDataString(aggregateId)}/state?sequenceNumber={sequenceNumber}";
        return await GetAsync<AggregateStateSnapshot>(path, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets full details of a specific event including its payload and metadata.
    /// </summary>
    public async Task<EventDetail?> GetEventDetailAsync(
        string tenantId,
        string domain,
        string aggregateId,
        long sequenceNumber,
        CancellationToken cancellationToken)
    {
        string path = $"/api/v1/admin/streams/{Uri.EscapeDataString(tenantId)}/{Uri.EscapeDataString(domain)}/{Uri.EscapeDataString(aggregateId)}/events/{sequenceNumber}";
        return await GetAsync<EventDetail>(path, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Diffs aggregate state between two sequence positions, showing which fields changed.
    /// </summary>
    public async Task<AggregateStateDiff?> DiffAggregateStateAsync(
        string tenantId,
        string domain,
        string aggregateId,
        long fromSequence,
        long toSequence,
        CancellationToken cancellationToken)
    {
        string path = $"/api/v1/admin/streams/{Uri.EscapeDataString(tenantId)}/{Uri.EscapeDataString(domain)}/{Uri.EscapeDataString(aggregateId)}/diff?fromSequence={fromSequence}&toSequence={toSequence}";
        return await GetAsync<AggregateStateDiff>(path, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Traces the full causation chain for an event at the specified sequence number.
    /// </summary>
    public async Task<CausationChain?> TraceCausationChainAsync(
        string tenantId,
        string domain,
        string aggregateId,
        long sequenceNumber,
        CancellationToken cancellationToken)
    {
        string path = $"/api/v1/admin/streams/{Uri.EscapeDataString(tenantId)}/{Uri.EscapeDataString(domain)}/{Uri.EscapeDataString(aggregateId)}/causation?sequenceNumber={sequenceNumber}";
        return await GetAsync<CausationChain>(path, cancellationToken).ConfigureAwait(false);
    }
}
