using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Hexalith.EventStore.Admin.Abstractions.Models.Dapr;
using Hexalith.EventStore.Admin.UI.Services.Exceptions;

using Microsoft.Extensions.Logging;

namespace Hexalith.EventStore.Admin.UI.Services;

/// <summary>
/// HTTP client for Admin.Server DAPR resiliency REST API endpoints.
/// </summary>
public class AdminResiliencyApiClient(
    IHttpClientFactory httpClientFactory,
    ILogger<AdminResiliencyApiClient> logger)
{
    /// <summary>
    /// Gets the DAPR resiliency specification.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The resiliency spec, or null on failure.</returns>
    public virtual async Task<DaprResiliencySpec?> GetResiliencySpecAsync(CancellationToken ct = default)
    {
        HttpClient client = httpClientFactory.CreateClient("AdminApi");
        try
        {
            using HttpResponseMessage response = await client
                .GetAsync("api/v1/admin/dapr/resiliency", ct)
                .ConfigureAwait(false);
            await HandleErrorStatusAsync(response).ConfigureAwait(false);
            return await response.Content
                .ReadFromJsonAsync<DaprResiliencySpec>(ct)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException
            and not ForbiddenAccessException
            and not InvalidOperationException
            and not ServiceUnavailableException
            and not OperationCanceledException)
        {
            logger.LogError(ex, "Failed to fetch DAPR resiliency spec");
            return null;
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
