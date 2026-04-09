using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text;

using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Abstractions.Models.Storage;
using Hexalith.EventStore.Admin.UI.Services.Exceptions;

using Microsoft.Extensions.Logging;

namespace Hexalith.EventStore.Admin.UI.Services;

/// <summary>
/// HTTP client for Admin.Server backup and restore REST API endpoints.
/// Wraps the "AdminApi" named HttpClient for backup operations.
/// </summary>
public class AdminBackupApiClient(
    IHttpClientFactory httpClientFactory,
    ILogger<AdminBackupApiClient> logger)
{
    /// <summary>
    /// Gets backup jobs, optionally filtered by tenant.
    /// </summary>
    /// <param name="tenantId">Optional tenant filter.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of backup jobs.</returns>
    public virtual async Task<IReadOnlyList<BackupJob>> GetBackupJobsAsync(
        string? tenantId = null,
        CancellationToken ct = default)
    {
        HttpClient client = httpClientFactory.CreateClient("AdminApi");
        string url = string.IsNullOrEmpty(tenantId)
            ? "api/v1/admin/backups"
            : $"api/v1/admin/backups?tenantId={Uri.EscapeDataString(tenantId)}";
        try
        {
            using HttpResponseMessage response = await client.GetAsync(url, ct).ConfigureAwait(false);
            await HandleErrorStatusAsync(response).ConfigureAwait(false);
            IReadOnlyList<BackupJob>? result = await response.Content
                .ReadFromJsonAsync<IReadOnlyList<BackupJob>>(ct)
                .ConfigureAwait(false);
            return result ?? [];
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException
            and not ForbiddenAccessException
            and not InvalidOperationException
            and not ServiceUnavailableException
            and not OperationCanceledException)
        {
            logger.LogError(ex, "Failed to fetch backup jobs from {Url}", url);
            throw new ServiceUnavailableException("Unable to load backup jobs.");
        }
    }

    /// <summary>
    /// Triggers a full tenant backup.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="description">Optional description.</param>
    /// <param name="includeSnapshots">Whether to include snapshots.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The operation result.</returns>
    public virtual async Task<AdminOperationResult?> TriggerBackupAsync(
        string tenantId,
        string? description = null,
        bool includeSnapshots = true,
        CancellationToken ct = default)
    {
        HttpClient client = httpClientFactory.CreateClient("AdminApi");
        StringBuilder urlBuilder = new();
        urlBuilder.Append($"api/v1/admin/backups/{Uri.EscapeDataString(tenantId)}?includeSnapshots={includeSnapshots}");
        if (!string.IsNullOrEmpty(description))
        {
            urlBuilder.Append($"&description={Uri.EscapeDataString(description)}");
        }

        string url = urlBuilder.ToString();
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
            logger.LogError(ex, "Failed to trigger backup at {Url}", url);
            throw new ServiceUnavailableException("Unable to trigger backup.");
        }
    }

    /// <summary>
    /// Validates integrity of a completed backup.
    /// </summary>
    /// <param name="backupId">The backup identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The operation result.</returns>
    public virtual async Task<AdminOperationResult?> ValidateBackupAsync(
        string backupId,
        CancellationToken ct = default)
    {
        HttpClient client = httpClientFactory.CreateClient("AdminApi");
        string url = $"api/v1/admin/backups/{Uri.EscapeDataString(backupId)}/validate";
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
            logger.LogError(ex, "Failed to validate backup at {Url}", url);
            throw new ServiceUnavailableException("Unable to validate backup.");
        }
    }

    /// <summary>
    /// Initiates a restore from a backup.
    /// </summary>
    /// <param name="backupId">The backup identifier.</param>
    /// <param name="pointInTime">Optional point-in-time cutoff.</param>
    /// <param name="dryRun">Whether to validate without applying.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The operation result.</returns>
    public virtual async Task<AdminOperationResult?> TriggerRestoreAsync(
        string backupId,
        DateTimeOffset? pointInTime = null,
        bool dryRun = false,
        CancellationToken ct = default)
    {
        HttpClient client = httpClientFactory.CreateClient("AdminApi");
        StringBuilder urlBuilder = new();
        urlBuilder.Append($"api/v1/admin/backups/{Uri.EscapeDataString(backupId)}/restore?dryRun={dryRun}");
        if (pointInTime.HasValue)
        {
            urlBuilder.Append($"&pointInTime={Uri.EscapeDataString(pointInTime.Value.ToString("o"))}");
        }

        string url = urlBuilder.ToString();
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
            logger.LogError(ex, "Failed to trigger restore at {Url}", url);
            throw new ServiceUnavailableException("Unable to trigger restore.");
        }
    }

    /// <summary>
    /// Exports a single stream as downloadable content.
    /// </summary>
    /// <param name="request">The export request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The export result with content.</returns>
    public virtual async Task<StreamExportResult?> ExportStreamAsync(
        StreamExportRequest request,
        CancellationToken ct = default)
    {
        HttpClient client = httpClientFactory.CreateClient("AdminApi");
        const string url = "api/v1/admin/backups/export-stream";
        try
        {
            using HttpResponseMessage response = await client.PostAsJsonAsync(url, request, ct).ConfigureAwait(false);
            await HandleErrorStatusAsync(response).ConfigureAwait(false);
            return await response.Content
                .ReadFromJsonAsync<StreamExportResult>(ct)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException
            and not ForbiddenAccessException
            and not InvalidOperationException
            and not ServiceUnavailableException
            and not OperationCanceledException)
        {
            logger.LogError(ex, "Failed to export stream at {Url}", url);
            throw new ServiceUnavailableException("Unable to export stream.");
        }
    }

    /// <summary>
    /// Imports events into a stream from exported content.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="content">The exported content to import.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The operation result.</returns>
    public virtual async Task<AdminOperationResult?> ImportStreamAsync(
        string tenantId,
        string content,
        CancellationToken ct = default)
    {
        HttpClient client = httpClientFactory.CreateClient("AdminApi");
        string url = $"api/v1/admin/backups/import-stream?tenantId={Uri.EscapeDataString(tenantId)}";
        try
        {
            using HttpResponseMessage response = await client.PostAsync(
                url,
                new StringContent(content, Encoding.UTF8, "application/json"),
                ct).ConfigureAwait(false);
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
            logger.LogError(ex, "Failed to import stream at {Url}", url);
            throw new ServiceUnavailableException("Unable to import stream.");
        }
    }

    private static async Task HandleErrorStatusAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.Accepted)
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
