using System.Net;
using System.Net.Http.Json;

using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Abstractions.Models.Health;
using Hexalith.EventStore.Admin.Abstractions.Models.Streams;
using Hexalith.EventStore.Admin.Abstractions.Models.Tenants;
using Hexalith.EventStore.Admin.Abstractions.Models.TypeCatalog;
using Hexalith.EventStore.Admin.UI.Services.Exceptions;

using Microsoft.Extensions.Logging;

namespace Hexalith.EventStore.Admin.UI.Services;

/// <summary>
/// HTTP client for Admin.Server REST API endpoints.
/// Wraps the "AdminApi" named HttpClient for streams, health, and tenants.
/// </summary>
public class AdminStreamApiClient(
    IHttpClientFactory httpClientFactory,
    ILogger<AdminStreamApiClient> logger)
{
    private static readonly PagedResult<StreamSummary> _emptyStreamsResult = new([], 0, null);

    /// <summary>
    /// Gets recently active streams, optionally filtered by tenant and domain.
    /// </summary>
    /// <param name="tenantId">Optional tenant filter.</param>
    /// <param name="domain">Optional domain filter.</param>
    /// <param name="count">Maximum number of streams to return.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A paged result of stream summaries.</returns>
    public virtual async Task<PagedResult<StreamSummary>> GetRecentlyActiveStreamsAsync(
        string? tenantId,
        string? domain,
        int count = 1000,
        CancellationToken ct = default)
    {
        HttpClient client = httpClientFactory.CreateClient("AdminApi");
        string url = BuildStreamsUrl(tenantId, domain, count);
        try
        {
            HttpResponseMessage response = await client.GetAsync(url, ct).ConfigureAwait(false);
            HandleErrorStatus(response);
            PagedResult<StreamSummary>? result = await response.Content
                .ReadFromJsonAsync<PagedResult<StreamSummary>>(ct)
                .ConfigureAwait(false);
            return result ?? _emptyStreamsResult;
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException
            and not ForbiddenAccessException
            and not ServiceUnavailableException
            and not OperationCanceledException)
        {
            logger.LogError(ex, "Failed to fetch streams from {Url}", url);
            return _emptyStreamsResult;
        }
    }

    /// <summary>
    /// Gets the overall system health report.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The system health report.</returns>
    public virtual async Task<SystemHealthReport?> GetSystemHealthAsync(CancellationToken ct = default)
    {
        HttpClient client = httpClientFactory.CreateClient("AdminApi");
        try
        {
            HttpResponseMessage response = await client
                .GetAsync("api/v1/admin/health", ct)
                .ConfigureAwait(false);
            HandleErrorStatus(response);
            return await response.Content
                .ReadFromJsonAsync<SystemHealthReport>(ct)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException
            and not ForbiddenAccessException
            and not ServiceUnavailableException
            and not OperationCanceledException)
        {
            logger.LogError(ex, "Failed to fetch system health");
            return null;
        }
    }

    /// <summary>
    /// Gets the list of tenants for filter dropdowns.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of tenant summaries.</returns>
    public virtual async Task<IReadOnlyList<TenantSummary>> GetTenantsAsync(CancellationToken ct = default)
    {
        HttpClient client = httpClientFactory.CreateClient("AdminApi");
        try
        {
            HttpResponseMessage response = await client
                .GetAsync("api/v1/admin/tenants", ct)
                .ConfigureAwait(false);
            HandleErrorStatus(response);
            IReadOnlyList<TenantSummary>? result = await response.Content
                .ReadFromJsonAsync<IReadOnlyList<TenantSummary>>(ct)
                .ConfigureAwait(false);
            return result ?? [];
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException
            and not ForbiddenAccessException
            and not ServiceUnavailableException
            and not OperationCanceledException)
        {
            logger.LogError(ex, "Failed to fetch tenants");
            return [];
        }
    }

    /// <summary>
    /// Gets the timeline for a specific stream.
    /// </summary>
    /// <param name="tenantId">Tenant identifier.</param>
    /// <param name="domain">Domain name.</param>
    /// <param name="aggregateId">Aggregate identifier.</param>
    /// <param name="fromSequence">Optional start sequence number.</param>
    /// <param name="toSequence">Optional end sequence number.</param>
    /// <param name="count">Maximum number of entries to return.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A paged result of timeline entries.</returns>
    public virtual async Task<PagedResult<TimelineEntry>> GetStreamTimelineAsync(
        string tenantId,
        string domain,
        string aggregateId,
        long? fromSequence = null,
        long? toSequence = null,
        int count = 50,
        CancellationToken ct = default)
    {
        HttpClient client = httpClientFactory.CreateClient("AdminApi");
        string url = BuildTimelineUrl(tenantId, domain, aggregateId, fromSequence, toSequence, count);
        try
        {
            HttpResponseMessage response = await client.GetAsync(url, ct).ConfigureAwait(false);
            HandleErrorStatus(response);
            PagedResult<TimelineEntry>? result = await response.Content
                .ReadFromJsonAsync<PagedResult<TimelineEntry>>(ct)
                .ConfigureAwait(false);
            return result ?? new PagedResult<TimelineEntry>([], 0, null);
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException
            and not ForbiddenAccessException
            and not ServiceUnavailableException
            and not OperationCanceledException)
        {
            logger.LogError(ex, "Failed to fetch timeline from {Url}", url);
            return new PagedResult<TimelineEntry>([], 0, null);
        }
    }

    /// <summary>
    /// Gets detailed information about a single event.
    /// </summary>
    /// <param name="tenantId">Tenant identifier.</param>
    /// <param name="domain">Domain name.</param>
    /// <param name="aggregateId">Aggregate identifier.</param>
    /// <param name="sequenceNumber">Event sequence number.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The event detail, or null if not found.</returns>
    public virtual async Task<EventDetail?> GetEventDetailAsync(
        string tenantId,
        string domain,
        string aggregateId,
        long sequenceNumber,
        CancellationToken ct = default)
    {
        HttpClient client = httpClientFactory.CreateClient("AdminApi");
        string url = $"api/v1/admin/streams/{Uri.EscapeDataString(tenantId)}/{Uri.EscapeDataString(domain)}/{Uri.EscapeDataString(aggregateId)}/events/{sequenceNumber}";
        try
        {
            HttpResponseMessage response = await client.GetAsync(url, ct).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            HandleErrorStatus(response);
            return await response.Content
                .ReadFromJsonAsync<EventDetail>(ct)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException
            and not ForbiddenAccessException
            and not ServiceUnavailableException
            and not OperationCanceledException)
        {
            logger.LogError(ex, "Failed to fetch event detail from {Url}", url);
            return null;
        }
    }

    /// <summary>
    /// Gets the aggregate state at a specific sequence position.
    /// </summary>
    /// <param name="tenantId">Tenant identifier.</param>
    /// <param name="domain">Domain name.</param>
    /// <param name="aggregateId">Aggregate identifier.</param>
    /// <param name="sequenceNumber">Sequence number at which to get the state.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The state snapshot, or null if not available at this position.</returns>
    public virtual async Task<AggregateStateSnapshot?> GetAggregateStateAtPositionAsync(
        string tenantId,
        string domain,
        string aggregateId,
        long sequenceNumber,
        CancellationToken ct = default)
    {
        HttpClient client = httpClientFactory.CreateClient("AdminApi");
        string url = $"api/v1/admin/streams/{Uri.EscapeDataString(tenantId)}/{Uri.EscapeDataString(domain)}/{Uri.EscapeDataString(aggregateId)}/state?sequenceNumber={sequenceNumber}";
        try
        {
            HttpResponseMessage response = await client.GetAsync(url, ct).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            HandleErrorStatus(response);
            return await response.Content
                .ReadFromJsonAsync<AggregateStateSnapshot>(ct)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException
            and not ForbiddenAccessException
            and not ServiceUnavailableException
            and not OperationCanceledException)
        {
            logger.LogError(ex, "Failed to fetch aggregate state from {Url}", url);
            return null;
        }
    }

    /// <summary>
    /// Traces the causation chain from a specific event.
    /// </summary>
    /// <param name="tenantId">Tenant identifier.</param>
    /// <param name="domain">Domain name.</param>
    /// <param name="aggregateId">Aggregate identifier.</param>
    /// <param name="sequenceNumber">Sequence number of the event to trace from.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The causation chain, or null if not found.</returns>
    public virtual async Task<CausationChain?> TraceCausationChainAsync(
        string tenantId,
        string domain,
        string aggregateId,
        long sequenceNumber,
        CancellationToken ct = default)
    {
        HttpClient client = httpClientFactory.CreateClient("AdminApi");
        string url = $"api/v1/admin/streams/{Uri.EscapeDataString(tenantId)}/{Uri.EscapeDataString(domain)}/{Uri.EscapeDataString(aggregateId)}/causation?sequenceNumber={sequenceNumber}";
        try
        {
            HttpResponseMessage response = await client.GetAsync(url, ct).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            HandleErrorStatus(response);
            return await response.Content
                .ReadFromJsonAsync<CausationChain>(ct)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException
            and not ForbiddenAccessException
            and not ServiceUnavailableException
            and not OperationCanceledException)
        {
            logger.LogError(ex, "Failed to fetch causation chain from {Url}", url);
            return null;
        }
    }

    /// <summary>
    /// Gets the registered aggregate types, optionally filtered by domain.
    /// </summary>
    /// <param name="domain">Optional domain filter.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of aggregate type information.</returns>
    public virtual async Task<IReadOnlyList<AggregateTypeInfo>> GetAggregateTypesAsync(
        string? domain = null,
        CancellationToken ct = default)
    {
        HttpClient client = httpClientFactory.CreateClient("AdminApi");
        string url = string.IsNullOrEmpty(domain)
            ? "api/v1/admin/types/aggregates"
            : $"api/v1/admin/types/aggregates?domain={Uri.EscapeDataString(domain)}";
        try
        {
            HttpResponseMessage response = await client.GetAsync(url, ct).ConfigureAwait(false);
            HandleErrorStatus(response);
            IReadOnlyList<AggregateTypeInfo>? result = await response.Content
                .ReadFromJsonAsync<IReadOnlyList<AggregateTypeInfo>>(ct)
                .ConfigureAwait(false);
            return result ?? [];
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException
            and not ForbiddenAccessException
            and not ServiceUnavailableException
            and not OperationCanceledException)
        {
            logger.LogError(ex, "Failed to fetch aggregate types from {Url}", url);
            return [];
        }
    }

    private static string BuildTimelineUrl(
        string tenantId,
        string domain,
        string aggregateId,
        long? fromSequence,
        long? toSequence,
        int count)
    {
        List<string> queryParams = [$"count={count}"];
        if (fromSequence.HasValue)
        {
            queryParams.Add($"fromSequence={fromSequence.Value}");
        }

        if (toSequence.HasValue)
        {
            queryParams.Add($"toSequence={toSequence.Value}");
        }

        return $"api/v1/admin/streams/{Uri.EscapeDataString(tenantId)}/{Uri.EscapeDataString(domain)}/{Uri.EscapeDataString(aggregateId)}/timeline?{string.Join('&', queryParams)}";
    }

    private static string BuildStreamsUrl(string? tenantId, string? domain, int count)
    {
        List<string> queryParams = [$"count={count}"];
        if (!string.IsNullOrEmpty(tenantId))
        {
            queryParams.Add($"tenantId={Uri.EscapeDataString(tenantId)}");
        }

        if (!string.IsNullOrEmpty(domain))
        {
            queryParams.Add($"domain={Uri.EscapeDataString(domain)}");
        }

        return $"api/v1/admin/streams?{string.Join('&', queryParams)}";
    }

    private static void HandleErrorStatus(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        // Dispose the response to return the connection to the pool
        HttpStatusCode statusCode = response.StatusCode;
        string? reasonPhrase = response.ReasonPhrase;
        response.Dispose();

        throw statusCode switch
        {
            HttpStatusCode.Unauthorized => new UnauthorizedAccessException(
                "Authentication required. Please sign in again."),
            HttpStatusCode.Forbidden => new ForbiddenAccessException(
                "Access denied. Insufficient permissions to access this resource."),
            HttpStatusCode.ServiceUnavailable => new ServiceUnavailableException(
                "The admin backend service is temporarily unavailable."),
            _ => new HttpRequestException(
                $"Admin API returned {(int)statusCode}: {reasonPhrase}",
                null,
                statusCode),
        };
    }
}
