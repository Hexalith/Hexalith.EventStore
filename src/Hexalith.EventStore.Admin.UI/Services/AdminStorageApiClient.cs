using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Hexalith.EventStore.Admin.Abstractions.Models.Storage;
using Hexalith.EventStore.Admin.UI.Services.Exceptions;

using Microsoft.Extensions.Logging;

namespace Hexalith.EventStore.Admin.UI.Services;

/// <summary>
/// HTTP client for Admin.Server storage REST API endpoints.
/// Wraps the "AdminApi" named HttpClient for storage overview and hot streams.
/// </summary>
public class AdminStorageApiClient(
    IHttpClientFactory httpClientFactory,
    ILogger<AdminStorageApiClient> logger)
{
    private static readonly StorageOverview _emptyOverview = new(0, null, []);

    /// <summary>
    /// Gets the storage overview, optionally filtered by tenant.
    /// </summary>
    /// <param name="tenantId">Optional tenant filter.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The storage overview.</returns>
    public virtual async Task<StorageOverview> GetStorageOverviewAsync(
        string? tenantId = null,
        CancellationToken ct = default)
    {
        HttpClient client = httpClientFactory.CreateClient("AdminApi");
        string url = string.IsNullOrEmpty(tenantId)
            ? "api/v1/admin/storage/overview"
            : $"api/v1/admin/storage/overview?tenantId={Uri.EscapeDataString(tenantId)}";
        try
        {
            using HttpResponseMessage response = await client.GetAsync(url, ct).ConfigureAwait(false);
            await HandleErrorStatusAsync(response).ConfigureAwait(false);
            StorageOverview? result = await response.Content
                .ReadFromJsonAsync<StorageOverview>(ct)
                .ConfigureAwait(false);
            return result ?? _emptyOverview;
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException
            and not ForbiddenAccessException
            and not InvalidOperationException
            and not ServiceUnavailableException
            and not OperationCanceledException)
        {
            logger.LogError(ex, "Failed to fetch storage overview from {Url}", url);
            throw new ServiceUnavailableException("Unable to load storage overview.");
        }
    }

    /// <summary>
    /// Gets the top hot streams by event count, optionally filtered by tenant.
    /// </summary>
    /// <param name="tenantId">Optional tenant filter.</param>
    /// <param name="count">Maximum number of streams to return.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of stream storage information.</returns>
    public virtual async Task<IReadOnlyList<StreamStorageInfo>> GetHotStreamsAsync(
        string? tenantId = null,
        int count = 100,
        CancellationToken ct = default)
    {
        HttpClient client = httpClientFactory.CreateClient("AdminApi");
        string url = BuildHotStreamsUrl(tenantId, count);
        try
        {
            using HttpResponseMessage response = await client.GetAsync(url, ct).ConfigureAwait(false);
            await HandleErrorStatusAsync(response).ConfigureAwait(false);
            IReadOnlyList<StreamStorageInfo>? result = await response.Content
                .ReadFromJsonAsync<IReadOnlyList<StreamStorageInfo>>(ct)
                .ConfigureAwait(false);
            return result ?? [];
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException
            and not ForbiddenAccessException
            and not InvalidOperationException
            and not ServiceUnavailableException
            and not OperationCanceledException)
        {
            logger.LogError(ex, "Failed to fetch hot streams from {Url}", url);
            throw new ServiceUnavailableException("Unable to load hot streams.");
        }
    }

    private static string BuildHotStreamsUrl(string? tenantId, int count)
    {
        List<string> queryParams = [$"count={count}"];
        if (!string.IsNullOrEmpty(tenantId))
        {
            queryParams.Add($"tenantId={Uri.EscapeDataString(tenantId)}");
        }

        return $"api/v1/admin/storage/hot-streams?{string.Join('&', queryParams)}";
    }

    private static async Task HandleErrorStatusAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        HttpStatusCode statusCode = response.StatusCode;
        string? reasonPhrase = response.ReasonPhrase;

        if (statusCode == HttpStatusCode.UnprocessableEntity)
        {
            string? errorDetail = null;
            try
            {
                string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                using JsonDocument doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("detail", out JsonElement detail))
                {
                    errorDetail = detail.GetString();
                }
            }
            catch
            {
                // Ignore parse failures — fall through to default message
            }

            throw new InvalidOperationException(
                errorDetail ?? reasonPhrase ?? "The operation was rejected by the server.");
        }

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
