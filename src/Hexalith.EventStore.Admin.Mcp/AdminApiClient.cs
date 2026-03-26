namespace Hexalith.EventStore.Admin.Mcp;

using System.Net.Http.Json;

using Hexalith.EventStore.Admin.Abstractions.Models.Health;

/// <summary>
/// Typed HttpClient wrapper for the EventStore Admin API.
/// </summary>
internal sealed class AdminApiClient
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
}
