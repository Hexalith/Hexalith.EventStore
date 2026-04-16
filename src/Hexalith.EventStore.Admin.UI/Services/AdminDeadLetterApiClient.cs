using System.Net;
using System.Text.Json;

using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Abstractions.Models.DeadLetters;
using Hexalith.EventStore.Admin.UI.Services.Exceptions;

namespace Hexalith.EventStore.Admin.UI.Services;

/// <summary>
/// HTTP client for Admin.Server dead-letter REST API endpoints.
/// Wraps the "AdminApi" named HttpClient for dead-letter query and command operations.
/// </summary>
public class AdminDeadLetterApiClient(
    IHttpClientFactory httpClientFactory,
    ILogger<AdminDeadLetterApiClient> logger) {
    /// <summary>
    /// Gets the total count of dead-letter entries across all tenants.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The dead-letter count, or null on failure.</returns>
    public virtual async Task<int?> GetDeadLetterCountAsync(CancellationToken ct = default) {
        HttpClient client = httpClientFactory.CreateClient("AdminApi");
        try {
            using HttpResponseMessage response = await client
                .GetAsync("api/v1/admin/dead-letters/count", ct)
                .ConfigureAwait(false);
            await HandleErrorStatusAsync(response).ConfigureAwait(false);
            return await response.Content
                .ReadFromJsonAsync<int>(ct)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException
            and not ForbiddenAccessException
            and not InvalidOperationException
            and not ServiceUnavailableException
            and not OperationCanceledException) {
            logger.LogError(ex, "Failed to fetch dead-letter count");
            return null;
        }
    }

    /// <summary>
    /// Gets dead-letter entries, optionally filtered by tenant, with pagination support.
    /// </summary>
    /// <param name="tenantId">Optional tenant filter.</param>
    /// <param name="count">Number of entries per page (default 100).</param>
    /// <param name="continuationToken">Opaque token for the next page.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A paged result of dead-letter entries.</returns>
    public virtual async Task<PagedResult<DeadLetterEntry>> GetDeadLettersAsync(
        string? tenantId = null,
        int count = 100,
        string? continuationToken = null,
        CancellationToken ct = default) {
        HttpClient client = httpClientFactory.CreateClient("AdminApi");
        List<string> queryParams = [];
        if (!string.IsNullOrEmpty(tenantId)) {
            queryParams.Add($"tenantId={Uri.EscapeDataString(tenantId)}");
        }

        if (count != 100) {
            queryParams.Add($"count={count}");
        }

        if (!string.IsNullOrEmpty(continuationToken)) {
            queryParams.Add($"continuationToken={Uri.EscapeDataString(continuationToken)}");
        }

        string url = queryParams.Count > 0
            ? $"api/v1/admin/dead-letters?{string.Join('&', queryParams)}"
            : "api/v1/admin/dead-letters";
        try {
            using HttpResponseMessage response = await client.GetAsync(url, ct).ConfigureAwait(false);
            await HandleErrorStatusAsync(response).ConfigureAwait(false);
            PagedResult<DeadLetterEntry>? result = await response.Content
                .ReadFromJsonAsync<PagedResult<DeadLetterEntry>>(ct)
                .ConfigureAwait(false);
            return result ?? new PagedResult<DeadLetterEntry>([], 0, null);
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException
            and not ForbiddenAccessException
            and not InvalidOperationException
            and not ServiceUnavailableException
            and not OperationCanceledException) {
            logger.LogError(ex, "Failed to fetch dead-letter entries from {Url}", url);
            throw new ServiceUnavailableException("Unable to load dead-letter entries.");
        }
    }

    /// <summary>
    /// Retries dead-letter entries for a specific tenant.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="messageIds">The message IDs to retry.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The operation result.</returns>
    public virtual async Task<AdminOperationResult?> RetryDeadLettersAsync(
        string tenantId,
        IReadOnlyList<string> messageIds,
        CancellationToken ct = default) {
        HttpClient client = httpClientFactory.CreateClient("AdminApi");
        string url = $"api/v1/admin/dead-letters/{Uri.EscapeDataString(tenantId)}/retry";
        try {
            using HttpResponseMessage response = await client
                .PostAsJsonAsync(url, new { MessageIds = messageIds }, ct)
                .ConfigureAwait(false);
            await HandleErrorStatusAsync(response).ConfigureAwait(false);
            return await response.Content
                .ReadFromJsonAsync<AdminOperationResult>(ct)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException
            and not ForbiddenAccessException
            and not InvalidOperationException
            and not ServiceUnavailableException
            and not OperationCanceledException) {
            logger.LogError(ex, "Failed to retry dead-letter entries at {Url}", url);
            throw new ServiceUnavailableException("Unable to retry dead-letter entries.");
        }
    }

    /// <summary>
    /// Skips dead-letter entries for a specific tenant.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="messageIds">The message IDs to skip.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The operation result.</returns>
    public virtual async Task<AdminOperationResult?> SkipDeadLettersAsync(
        string tenantId,
        IReadOnlyList<string> messageIds,
        CancellationToken ct = default) {
        HttpClient client = httpClientFactory.CreateClient("AdminApi");
        string url = $"api/v1/admin/dead-letters/{Uri.EscapeDataString(tenantId)}/skip";
        try {
            using HttpResponseMessage response = await client
                .PostAsJsonAsync(url, new { MessageIds = messageIds }, ct)
                .ConfigureAwait(false);
            await HandleErrorStatusAsync(response).ConfigureAwait(false);
            return await response.Content
                .ReadFromJsonAsync<AdminOperationResult>(ct)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException
            and not ForbiddenAccessException
            and not InvalidOperationException
            and not ServiceUnavailableException
            and not OperationCanceledException) {
            logger.LogError(ex, "Failed to skip dead-letter entries at {Url}", url);
            throw new ServiceUnavailableException("Unable to skip dead-letter entries.");
        }
    }

    /// <summary>
    /// Archives dead-letter entries for a specific tenant.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="messageIds">The message IDs to archive.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The operation result.</returns>
    public virtual async Task<AdminOperationResult?> ArchiveDeadLettersAsync(
        string tenantId,
        IReadOnlyList<string> messageIds,
        CancellationToken ct = default) {
        HttpClient client = httpClientFactory.CreateClient("AdminApi");
        string url = $"api/v1/admin/dead-letters/{Uri.EscapeDataString(tenantId)}/archive";
        try {
            using HttpResponseMessage response = await client
                .PostAsJsonAsync(url, new { MessageIds = messageIds }, ct)
                .ConfigureAwait(false);
            await HandleErrorStatusAsync(response).ConfigureAwait(false);
            return await response.Content
                .ReadFromJsonAsync<AdminOperationResult>(ct)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException
            and not ForbiddenAccessException
            and not InvalidOperationException
            and not ServiceUnavailableException
            and not OperationCanceledException) {
            logger.LogError(ex, "Failed to archive dead-letter entries at {Url}", url);
            throw new ServiceUnavailableException("Unable to archive dead-letter entries.");
        }
    }

    private static async Task HandleErrorStatusAsync(HttpResponseMessage response) {
        if (response.IsSuccessStatusCode) {
            return;
        }

        HttpStatusCode statusCode = response.StatusCode;
        string? reasonPhrase = response.ReasonPhrase;

        if (statusCode == HttpStatusCode.UnprocessableEntity) {
            string? errorDetail = null;
            try {
                string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("detail", out JsonElement detail)) {
                    errorDetail = detail.GetString();
                }
            }
            catch {
                // Ignore parse failures — fall through to default message
            }

            throw new InvalidOperationException(
                errorDetail ?? reasonPhrase ?? "The operation was rejected by the server.");
        }

        throw statusCode switch {
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
