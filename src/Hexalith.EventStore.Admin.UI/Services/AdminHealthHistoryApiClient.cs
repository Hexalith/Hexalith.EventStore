using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Hexalith.EventStore.Admin.Abstractions.Models.Dapr;
using Hexalith.EventStore.Admin.UI.Services.Exceptions;

using Microsoft.Extensions.Logging;

namespace Hexalith.EventStore.Admin.UI.Services;

/// <summary>
/// HTTP client for Admin.Server DAPR health history REST API endpoints.
/// </summary>
public class AdminHealthHistoryApiClient(
    IHttpClientFactory httpClientFactory,
    ILogger<AdminHealthHistoryApiClient> logger)
{
    /// <summary>
    /// Gets DAPR component health history for a time range.
    /// </summary>
    /// <param name="from">Start of the time range.</param>
    /// <param name="to">End of the time range.</param>
    /// <param name="component">Optional component name filter.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The health timeline, or null on failure.</returns>
    public virtual async Task<DaprComponentHealthTimeline?> GetHealthHistoryAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        string? component = null,
        CancellationToken ct = default)
    {
        HttpClient client = httpClientFactory.CreateClient("AdminApi");
        try
        {
            string url = $"api/v1/admin/health/dapr/history?from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(to.ToString("O"))}";
            if (!string.IsNullOrEmpty(component))
            {
                url += $"&component={Uri.EscapeDataString(component)}";
            }

            using HttpResponseMessage response = await client
                .GetAsync(url, ct)
                .ConfigureAwait(false);
            await HandleErrorStatusAsync(response).ConfigureAwait(false);
            return await response.Content
                .ReadFromJsonAsync<DaprComponentHealthTimeline>(ct)
                .ConfigureAwait(false);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotImplemented)
        {
            // Server does not support health history (collection disabled) — signal via null
            logger.LogDebug(ex, "Health history not available on server");
            return null;
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException
            and not ForbiddenAccessException
            and not InvalidOperationException
            and not ServiceUnavailableException
            and not OperationCanceledException)
        {
            logger.LogWarning(ex, "Failed to fetch health history");
            throw;
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
