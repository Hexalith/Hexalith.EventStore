using System.Net;
using System.Text.Json;

using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Abstractions.Models.Projections;
using Hexalith.EventStore.Admin.UI.Services.Exceptions;

namespace Hexalith.EventStore.Admin.UI.Services;

/// <summary>
/// HTTP client for Admin.Server projection REST API endpoints.
/// Wraps the "AdminApi" named HttpClient for projection operations.
/// </summary>
public class AdminProjectionApiClient(
    IHttpClientFactory httpClientFactory,
    ILogger<AdminProjectionApiClient> logger) {
    /// <summary>
    /// Gets all projections, optionally filtered by tenant.
    /// </summary>
    /// <param name="tenantId">Optional tenant filter.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of projection statuses.</returns>
    public virtual async Task<IReadOnlyList<ProjectionStatus>> ListProjectionsAsync(
        string? tenantId,
        CancellationToken ct = default) {
        HttpClient client = httpClientFactory.CreateClient("AdminApi");
        string url = string.IsNullOrEmpty(tenantId)
            ? "api/v1/admin/projections"
            : $"api/v1/admin/projections?tenantId={Uri.EscapeDataString(tenantId)}";
        try {
            using HttpResponseMessage response = await client.GetAsync(url, ct).ConfigureAwait(false);
            await HandleErrorStatusAsync(response).ConfigureAwait(false);
            IReadOnlyList<ProjectionStatus>? result = await response.Content
                .ReadFromJsonAsync<IReadOnlyList<ProjectionStatus>>(ct)
                .ConfigureAwait(false);
            return result ?? [];
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException
            and not ForbiddenAccessException
            and not InvalidOperationException
            and not ServiceUnavailableException
            and not OperationCanceledException) {
            logger.LogError(ex, "Failed to fetch projections from {Url}", url);
            return [];
        }
    }

    /// <summary>
    /// Gets detailed information about a specific projection.
    /// </summary>
    /// <param name="tenantId">Tenant identifier.</param>
    /// <param name="projectionName">Projection name.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The projection detail, or null if not found.</returns>
    public virtual async Task<ProjectionDetail?> GetProjectionDetailAsync(
        string tenantId,
        string projectionName,
        CancellationToken ct = default) {
        HttpClient client = httpClientFactory.CreateClient("AdminApi");
        string url = $"api/v1/admin/projections/{Uri.EscapeDataString(tenantId)}/{Uri.EscapeDataString(projectionName)}";
        try {
            using HttpResponseMessage response = await client.GetAsync(url, ct).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.NotFound) {
                return null;
            }

            await HandleErrorStatusAsync(response).ConfigureAwait(false);
            return await response.Content
                .ReadFromJsonAsync<ProjectionDetail>(ct)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException
            and not ForbiddenAccessException
            and not InvalidOperationException
            and not ServiceUnavailableException
            and not OperationCanceledException) {
            logger.LogError(ex, "Failed to fetch projection detail from {Url}", url);
            return null;
        }
    }

    /// <summary>
    /// Pauses a running projection.
    /// </summary>
    /// <param name="tenantId">Tenant identifier.</param>
    /// <param name="projectionName">Projection name.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The operation result, or null on failure.</returns>
    public virtual async Task<AdminOperationResult?> PauseProjectionAsync(
        string tenantId,
        string projectionName,
        CancellationToken ct = default) {
        HttpClient client = httpClientFactory.CreateClient("AdminApi");
        string url = $"api/v1/admin/projections/{Uri.EscapeDataString(tenantId)}/{Uri.EscapeDataString(projectionName)}/pause";
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
            logger.LogError(ex, "Failed to pause projection at {Url}", url);
            return null;
        }
    }

    /// <summary>
    /// Resumes a paused projection.
    /// </summary>
    /// <param name="tenantId">Tenant identifier.</param>
    /// <param name="projectionName">Projection name.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The operation result, or null on failure.</returns>
    public virtual async Task<AdminOperationResult?> ResumeProjectionAsync(
        string tenantId,
        string projectionName,
        CancellationToken ct = default) {
        HttpClient client = httpClientFactory.CreateClient("AdminApi");
        string url = $"api/v1/admin/projections/{Uri.EscapeDataString(tenantId)}/{Uri.EscapeDataString(projectionName)}/resume";
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
            logger.LogError(ex, "Failed to resume projection at {Url}", url);
            return null;
        }
    }

    /// <summary>
    /// Resets a projection, optionally from a specific position.
    /// </summary>
    /// <param name="tenantId">Tenant identifier.</param>
    /// <param name="projectionName">Projection name.</param>
    /// <param name="fromPosition">Optional position to reset from (null = from beginning).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The operation result, or null on failure.</returns>
    public virtual async Task<AdminOperationResult?> ResetProjectionAsync(
        string tenantId,
        string projectionName,
        long? fromPosition,
        CancellationToken ct = default) {
        HttpClient client = httpClientFactory.CreateClient("AdminApi");
        string url = $"api/v1/admin/projections/{Uri.EscapeDataString(tenantId)}/{Uri.EscapeDataString(projectionName)}/reset";
        try {
            using var content = JsonContent.Create(new { fromPosition });
            using HttpResponseMessage response = await client
                .PostAsync(url, content, ct)
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
            logger.LogError(ex, "Failed to reset projection at {Url}", url);
            return null;
        }
    }

    /// <summary>
    /// Replays events between two positions for a projection.
    /// </summary>
    /// <param name="tenantId">Tenant identifier.</param>
    /// <param name="projectionName">Projection name.</param>
    /// <param name="fromPosition">Starting position (inclusive).</param>
    /// <param name="toPosition">Ending position (inclusive).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The operation result, or null on failure.</returns>
    public virtual async Task<AdminOperationResult?> ReplayProjectionAsync(
        string tenantId,
        string projectionName,
        long fromPosition,
        long toPosition,
        CancellationToken ct = default) {
        HttpClient client = httpClientFactory.CreateClient("AdminApi");
        string url = $"api/v1/admin/projections/{Uri.EscapeDataString(tenantId)}/{Uri.EscapeDataString(projectionName)}/replay";
        try {
            using var content = JsonContent.Create(new { fromPosition, toPosition });
            using HttpResponseMessage response = await client
                .PostAsync(url, content, ct)
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
            logger.LogError(ex, "Failed to replay projection at {Url}", url);
            return null;
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
