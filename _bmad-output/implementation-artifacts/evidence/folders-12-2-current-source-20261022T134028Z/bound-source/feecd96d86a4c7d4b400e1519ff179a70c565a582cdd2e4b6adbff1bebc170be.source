using System.Net;
using System.Text.Json;

using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Abstractions.Models.Consistency;
using Hexalith.EventStore.Admin.UI.Services.Exceptions;

namespace Hexalith.EventStore.Admin.UI.Services;

/// <summary>
/// HTTP client for Admin.Server consistency check REST API endpoints.
/// Wraps the "AdminApi" named HttpClient for consistency check operations.
/// </summary>
public class AdminConsistencyApiClient(
    IHttpClientFactory httpClientFactory,
    ILogger<AdminConsistencyApiClient> logger) {
    /// <summary>
    /// Gets the full result of a consistency check including anomaly details.
    /// </summary>
    /// <param name="checkId">The check identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The full check result, or null if not found.</returns>
    public virtual async Task<ConsistencyCheckResult?> GetCheckResultAsync(
        string checkId,
        CancellationToken ct = default) {
        HttpClient client = httpClientFactory.CreateClient("AdminApi");
        string url = $"api/v1/admin/consistency/checks/{Uri.EscapeDataString(checkId)}";
        try {
            using HttpResponseMessage response = await client.GetAsync(url, ct).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.NotFound) {
                return null;
            }

            await HandleErrorStatusAsync(response).ConfigureAwait(false);
            return await response.Content
                .ReadFromJsonAsync<ConsistencyCheckResult>(ct)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException
            and not ForbiddenAccessException
            and not InvalidOperationException
            and not ServiceUnavailableException
            and not OperationCanceledException) {
            logger.LogError(ex, "Failed to fetch consistency check result from {Url}", url);
            throw new ServiceUnavailableException("Unable to load consistency check result.");
        }
    }

    /// <summary>
    /// Gets consistency check summaries, optionally filtered by tenant.
    /// </summary>
    /// <param name="tenantId">Optional tenant filter.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of consistency check summaries.</returns>
    public virtual async Task<IReadOnlyList<ConsistencyCheckSummary>> GetChecksAsync(
        string? tenantId = null,
        CancellationToken ct = default) {
        HttpClient client = httpClientFactory.CreateClient("AdminApi");
        string url = string.IsNullOrEmpty(tenantId)
            ? "api/v1/admin/consistency/checks"
            : $"api/v1/admin/consistency/checks?tenantId={Uri.EscapeDataString(tenantId)}";
        try {
            using HttpResponseMessage response = await client.GetAsync(url, ct).ConfigureAwait(false);
            await HandleErrorStatusAsync(response).ConfigureAwait(false);
            IReadOnlyList<ConsistencyCheckSummary>? result = await response.Content
                .ReadFromJsonAsync<IReadOnlyList<ConsistencyCheckSummary>>(ct)
                .ConfigureAwait(false);
            return result ?? [];
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException
            and not ForbiddenAccessException
            and not InvalidOperationException
            and not ServiceUnavailableException
            and not OperationCanceledException) {
            logger.LogError(ex, "Failed to fetch consistency checks from {Url}", url);
            throw new ServiceUnavailableException("Unable to load consistency checks.");
        }
    }

    /// <summary>
    /// Triggers a new consistency check.
    /// </summary>
    /// <param name="tenantId">Optional tenant scope.</param>
    /// <param name="domain">Optional domain scope.</param>
    /// <param name="checkTypes">Types of checks to perform.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The operation result.</returns>
    public virtual async Task<AdminOperationResult?> TriggerCheckAsync(
        string? tenantId,
        string? domain,
        IReadOnlyList<ConsistencyCheckType> checkTypes,
        CancellationToken ct = default) {
        HttpClient client = httpClientFactory.CreateClient("AdminApi");
        string url = "api/v1/admin/consistency/checks";
        try {
            var request = new { TenantId = tenantId, Domain = domain, CheckTypes = checkTypes };
            using HttpResponseMessage response = await client
                .PostAsJsonAsync(url, request, ct)
                .ConfigureAwait(false);
            await HandleErrorStatusAsync(response).ConfigureAwait(false);
            return await response.Content
                .ReadFromJsonAsync<AdminOperationResult>(ct)
                .ConfigureAwait(false);
        }
        catch (InvalidOperationException) {
            throw;
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException
            and not ForbiddenAccessException
            and not InvalidOperationException
            and not ServiceUnavailableException
            and not OperationCanceledException) {
            logger.LogError(ex, "Failed to trigger consistency check at {Url}", url);
            throw new ServiceUnavailableException("Unable to trigger consistency check.");
        }
    }

    /// <summary>
    /// Cancels a running consistency check.
    /// </summary>
    /// <param name="checkId">The check identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The operation result.</returns>
    public virtual async Task<AdminOperationResult?> CancelCheckAsync(
        string checkId,
        CancellationToken ct = default) {
        HttpClient client = httpClientFactory.CreateClient("AdminApi");
        string url = $"api/v1/admin/consistency/checks/{Uri.EscapeDataString(checkId)}/cancel";
        try {
            using HttpResponseMessage response = await client
                .PostAsync(url, null, ct)
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
            logger.LogError(ex, "Failed to cancel consistency check at {Url}", url);
            throw new ServiceUnavailableException("Unable to cancel consistency check.");
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
            HttpStatusCode.Conflict => new InvalidOperationException(
                "A consistency check is already running for this tenant. Wait for it to complete or cancel it."),
            HttpStatusCode.ServiceUnavailable => new ServiceUnavailableException(
                "The admin backend service is temporarily unavailable."),
            _ => new HttpRequestException(
                $"Admin API returned {(int)statusCode}: {reasonPhrase}",
                null,
                statusCode),
        };
    }
}
