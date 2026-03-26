using System.Net;
using System.Net.Http.Json;

using Hexalith.EventStore.Admin.Abstractions.Models.Dapr;
using Hexalith.EventStore.Admin.UI.Services.Exceptions;

using Microsoft.Extensions.Logging;

namespace Hexalith.EventStore.Admin.UI.Services;

/// <summary>
/// HTTP client for Admin.Server DAPR infrastructure REST API endpoints.
/// </summary>
public class AdminDaprApiClient(
    IHttpClientFactory httpClientFactory,
    ILogger<AdminDaprApiClient> logger)
{
    /// <summary>
    /// Gets detailed information about all registered DAPR components.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of DAPR component details, or an empty list on failure.</returns>
    public virtual async Task<IReadOnlyList<DaprComponentDetail>> GetComponentsAsync(CancellationToken ct = default)
    {
        HttpClient client = httpClientFactory.CreateClient("AdminApi");
        try
        {
            using HttpResponseMessage response = await client
                .GetAsync("api/v1/admin/dapr/components", ct)
                .ConfigureAwait(false);
            HandleErrorStatus(response);
            IReadOnlyList<DaprComponentDetail>? result = await response.Content
                .ReadFromJsonAsync<IReadOnlyList<DaprComponentDetail>>(ct)
                .ConfigureAwait(false);
            return result ?? [];
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException
            and not ForbiddenAccessException
            and not ServiceUnavailableException
            and not OperationCanceledException)
        {
            logger.LogError(ex, "Failed to fetch DAPR components");
            return [];
        }
    }

    /// <summary>
    /// Gets summary information about the DAPR sidecar runtime.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The sidecar info, or null on failure.</returns>
    public virtual async Task<DaprSidecarInfo?> GetSidecarInfoAsync(CancellationToken ct = default)
    {
        HttpClient client = httpClientFactory.CreateClient("AdminApi");
        try
        {
            using HttpResponseMessage response = await client
                .GetAsync("api/v1/admin/dapr/sidecar", ct)
                .ConfigureAwait(false);
            HandleErrorStatus(response);
            return await response.Content
                .ReadFromJsonAsync<DaprSidecarInfo>(ct)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException
            and not ForbiddenAccessException
            and not ServiceUnavailableException
            and not OperationCanceledException)
        {
            logger.LogError(ex, "Failed to fetch DAPR sidecar info");
            return null;
        }
    }

    private static void HandleErrorStatus(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        HttpStatusCode statusCode = response.StatusCode;
        string? reasonPhrase = response.ReasonPhrase;

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
