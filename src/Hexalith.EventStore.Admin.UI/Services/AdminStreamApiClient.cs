using System.Net;
using System.Net.Http.Json;

using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Abstractions.Models.Health;
using Hexalith.EventStore.Admin.Abstractions.Models.Streams;
using Hexalith.EventStore.Admin.Abstractions.Models.Tenants;
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

        throw response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => new UnauthorizedAccessException(
                "Authentication required. Please sign in again."),
            HttpStatusCode.Forbidden => new ForbiddenAccessException(
                "Access denied. Insufficient permissions to access this resource."),
            HttpStatusCode.ServiceUnavailable => new ServiceUnavailableException(
                "The admin backend service is temporarily unavailable."),
            _ => new HttpRequestException(
                $"Admin API returned {(int)response.StatusCode}: {response.ReasonPhrase}",
                null,
                response.StatusCode),
        };
    }
}
