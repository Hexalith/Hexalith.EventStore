using System.Net;
using System.Text.Json;

using Hexalith.EventStore.Admin.Abstractions.Models.TypeCatalog;
using Hexalith.EventStore.Admin.UI.Services.Exceptions;

namespace Hexalith.EventStore.Admin.UI.Services;

/// <summary>
/// HTTP client for Admin.Server type catalog REST API endpoints.
/// Wraps the "AdminApi" named HttpClient for type catalog operations.
/// </summary>
public class AdminTypeCatalogApiClient(
    IHttpClientFactory httpClientFactory,
    ILogger<AdminTypeCatalogApiClient> logger) {
    /// <summary>
    /// Gets all registered event types, optionally filtered by domain.
    /// </summary>
    /// <param name="domain">Optional domain filter.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of event type information.</returns>
    public virtual async Task<IReadOnlyList<EventTypeInfo>> ListEventTypesAsync(
        string? domain,
        CancellationToken ct = default) {
        HttpClient client = httpClientFactory.CreateClient("AdminApi");
        string url = string.IsNullOrEmpty(domain)
            ? "api/v1/admin/types/events"
            : $"api/v1/admin/types/events?domain={Uri.EscapeDataString(domain)}";
        try {
            using HttpResponseMessage response = await client.GetAsync(url, ct).ConfigureAwait(false);
            await HandleErrorStatusAsync(response).ConfigureAwait(false);
            IReadOnlyList<EventTypeInfo>? result = await response.Content
                .ReadFromJsonAsync<IReadOnlyList<EventTypeInfo>>(ct)
                .ConfigureAwait(false);
            return result ?? [];
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException
            and not ForbiddenAccessException
            and not InvalidOperationException
            and not ServiceUnavailableException
            and not OperationCanceledException) {
            logger.LogError(ex, "Failed to fetch event types from {Url}", url);
            return [];
        }
    }

    /// <summary>
    /// Gets all registered command types, optionally filtered by domain.
    /// </summary>
    /// <param name="domain">Optional domain filter.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of command type information.</returns>
    public virtual async Task<IReadOnlyList<CommandTypeInfo>> ListCommandTypesAsync(
        string? domain,
        CancellationToken ct = default) {
        HttpClient client = httpClientFactory.CreateClient("AdminApi");
        string url = string.IsNullOrEmpty(domain)
            ? "api/v1/admin/types/commands"
            : $"api/v1/admin/types/commands?domain={Uri.EscapeDataString(domain)}";
        try {
            using HttpResponseMessage response = await client.GetAsync(url, ct).ConfigureAwait(false);
            await HandleErrorStatusAsync(response).ConfigureAwait(false);
            IReadOnlyList<CommandTypeInfo>? result = await response.Content
                .ReadFromJsonAsync<IReadOnlyList<CommandTypeInfo>>(ct)
                .ConfigureAwait(false);
            return result ?? [];
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException
            and not ForbiddenAccessException
            and not InvalidOperationException
            and not ServiceUnavailableException
            and not OperationCanceledException) {
            logger.LogError(ex, "Failed to fetch command types from {Url}", url);
            return [];
        }
    }

    /// <summary>
    /// Gets all registered aggregate types, optionally filtered by domain.
    /// </summary>
    /// <param name="domain">Optional domain filter.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of aggregate type information.</returns>
    public virtual async Task<IReadOnlyList<AggregateTypeInfo>> ListAggregateTypesAsync(
        string? domain,
        CancellationToken ct = default) {
        HttpClient client = httpClientFactory.CreateClient("AdminApi");
        string url = string.IsNullOrEmpty(domain)
            ? "api/v1/admin/types/aggregates"
            : $"api/v1/admin/types/aggregates?domain={Uri.EscapeDataString(domain)}";
        try {
            using HttpResponseMessage response = await client.GetAsync(url, ct).ConfigureAwait(false);
            await HandleErrorStatusAsync(response).ConfigureAwait(false);
            IReadOnlyList<AggregateTypeInfo>? result = await response.Content
                .ReadFromJsonAsync<IReadOnlyList<AggregateTypeInfo>>(ct)
                .ConfigureAwait(false);
            return result ?? [];
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException
            and not ForbiddenAccessException
            and not InvalidOperationException
            and not ServiceUnavailableException
            and not OperationCanceledException) {
            logger.LogError(ex, "Failed to fetch aggregate types from {Url}", url);
            return [];
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
