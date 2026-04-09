using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Abstractions.Models.Storage;
using Hexalith.EventStore.Admin.UI.Services.Exceptions;

using Microsoft.Extensions.Logging;

namespace Hexalith.EventStore.Admin.UI.Services;

/// <summary>
/// HTTP client for Admin.Server compaction REST API endpoints.
/// Wraps the "AdminApi" named HttpClient for compaction jobs and trigger operations.
/// </summary>
public class AdminCompactionApiClient(
    IHttpClientFactory httpClientFactory,
    ILogger<AdminCompactionApiClient> logger)
{
    /// <summary>
    /// Gets the compaction jobs, optionally filtered by tenant.
    /// </summary>
    /// <param name="tenantId">Optional tenant filter.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of compaction jobs.</returns>
    public virtual async Task<IReadOnlyList<CompactionJob>> GetCompactionJobsAsync(
        string? tenantId = null,
        CancellationToken ct = default)
    {
        HttpClient client = httpClientFactory.CreateClient("AdminApi");
        string url = string.IsNullOrEmpty(tenantId)
            ? "api/v1/admin/storage/compaction-jobs"
            : $"api/v1/admin/storage/compaction-jobs?tenantId={Uri.EscapeDataString(tenantId)}";
        try
        {
            using HttpResponseMessage response = await client.GetAsync(url, ct).ConfigureAwait(false);
            await HandleErrorStatusAsync(response).ConfigureAwait(false);
            IReadOnlyList<CompactionJob>? result = await response.Content
                .ReadFromJsonAsync<IReadOnlyList<CompactionJob>>(ct)
                .ConfigureAwait(false);
            return result ?? [];
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException
            and not ForbiddenAccessException
            and not InvalidOperationException
            and not ServiceUnavailableException
            and not OperationCanceledException)
        {
            logger.LogError(ex, "Failed to fetch compaction jobs from {Url}", url);
            throw new ServiceUnavailableException("Unable to load compaction jobs.");
        }
    }

    /// <summary>
    /// Triggers compaction for a tenant, optionally scoped to a specific domain.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="domain">Optional domain scope.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The operation result.</returns>
    public virtual async Task<AdminOperationResult?> TriggerCompactionAsync(
        string tenantId,
        string? domain = null,
        CancellationToken ct = default)
    {
        HttpClient client = httpClientFactory.CreateClient("AdminApi");
        string url = string.IsNullOrEmpty(domain)
            ? $"api/v1/admin/storage/{Uri.EscapeDataString(tenantId)}/compact"
            : $"api/v1/admin/storage/{Uri.EscapeDataString(tenantId)}/compact?domain={Uri.EscapeDataString(domain)}";
        try
        {
            using HttpResponseMessage response = await client.PostAsync(url, null, ct).ConfigureAwait(false);
            await HandleErrorStatusAsync(response).ConfigureAwait(false);
            return await response.Content
                .ReadFromJsonAsync<AdminOperationResult>(ct)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException
            and not ForbiddenAccessException
            and not InvalidOperationException
            and not ServiceUnavailableException
            and not OperationCanceledException)
        {
            logger.LogError(ex, "Failed to trigger compaction at {Url}", url);
            throw new ServiceUnavailableException("Unable to trigger compaction.");
        }
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
