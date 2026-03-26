namespace Hexalith.EventStore.Admin.Mcp;

using System.Net.Http.Json;

using Hexalith.EventStore.Admin.Abstractions.Models.Health;

/// <summary>
/// Typed HttpClient wrapper for the EventStore Admin API.
/// </summary>
internal sealed partial class AdminApiClient
{
    private readonly HttpClient _httpClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdminApiClient"/> class.
    /// </summary>
    /// <param name="httpClient">The configured <see cref="HttpClient"/>.</param>
    public AdminApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    /// <summary>
    /// Retrieves the system health report from the Admin API.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The system health report.</returns>
    public async Task<SystemHealthReport?> GetSystemHealthAsync(CancellationToken cancellationToken)
    {
        return await _httpClient
            .GetFromJsonAsync<SystemHealthReport>("/api/v1/admin/health", cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Sends a GET request and deserializes the response as JSON.
    /// Delegates to <see cref="HttpClient.GetFromJsonAsync{T}(string, CancellationToken)"/>,
    /// which may throw <see cref="HttpRequestException"/> on non-success status codes
    /// or <see cref="System.Text.Json.JsonException"/> on malformed response bodies.
    /// </summary>
    /// <typeparam name="T">The type to deserialize to.</typeparam>
    /// <param name="path">The request URI path with query string.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The deserialized response, or <c>null</c> if the JSON body is the literal <c>null</c>.</returns>
    internal async Task<T?> GetAsync<T>(string path, CancellationToken cancellationToken)
    {
        return await _httpClient
            .GetFromJsonAsync<T>(path, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Sends a GET request for a list endpoint, returning an empty list when the response is null or empty.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="path">The request URI path with query string.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The deserialized list, or an empty list.</returns>
    internal async Task<IReadOnlyList<T>> GetListAsync<T>(string path, CancellationToken cancellationToken)
    {
        IReadOnlyList<T>? result = await GetAsync<IReadOnlyList<T>>(path, cancellationToken).ConfigureAwait(false);
        return result ?? Array.Empty<T>();
    }
}
