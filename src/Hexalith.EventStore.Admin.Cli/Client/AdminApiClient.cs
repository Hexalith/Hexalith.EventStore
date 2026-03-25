using System.Net.Http.Headers;
using System.Text.Json;

using Hexalith.EventStore.Admin.Cli.Formatting;

namespace Hexalith.EventStore.Admin.Cli.Client;

/// <summary>
/// HTTP client wrapper for calling the Admin REST API.
/// </summary>
public class AdminApiClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly bool _ownsClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdminApiClient"/> class with options from global CLI options.
    /// </summary>
    public AdminApiClient(GlobalOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(options.Url),
            Timeout = TimeSpan.FromSeconds(10),
        };
        if (options.Token is not null)
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.Token);
        }

        _ownsClient = true;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AdminApiClient"/> class with options and a custom handler (for testing).
    /// </summary>
    internal AdminApiClient(GlobalOptions options, HttpMessageHandler handler)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(handler);

        _httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri(options.Url),
            Timeout = TimeSpan.FromSeconds(10),
        };

        if (options.Token is not null)
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.Token);
        }

        _ownsClient = true;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AdminApiClient"/> class with a pre-configured HTTP client (for testing).
    /// </summary>
    internal AdminApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _ownsClient = false;
    }

    /// <summary>
    /// Sends a GET request and deserializes the JSON response.
    /// </summary>
    /// <exception cref="AdminApiException">Thrown for all API communication errors with user-friendly messages.</exception>
    public async Task<T> GetAsync<T>(string path, CancellationToken cancellationToken)
    {
        string resolvedUrl = _httpClient.BaseAddress?.ToString().TrimEnd('/') ?? "unknown";
        HttpResponseMessage response;
        try
        {
            response = await _httpClient.GetAsync(path, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex) when (ex.InnerException is System.Net.Sockets.SocketException)
        {
            throw new AdminApiException($"Cannot connect to Admin API at {resolvedUrl}. Is the server running?", ex);
        }
        catch (HttpRequestException ex)
        {
            throw new AdminApiException($"Cannot connect to Admin API at {resolvedUrl}. Is the server running?", ex);
        }
        catch (TaskCanceledException ex)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw new AdminApiException($"Request was canceled. (URL: {resolvedUrl})", ex);
            }

            throw new AdminApiException($"Request timed out after 10 seconds. (URL: {resolvedUrl})", ex);
        }

        int statusCode = (int)response.StatusCode;
        switch (statusCode)
        {
            case 401:
                throw new AdminApiException($"Authentication required. Use --token to provide a JWT token. (URL: {resolvedUrl})");
            case 403:
                throw new AdminApiException($"Access denied. Insufficient permissions. (URL: {resolvedUrl})");
            case 404:
                throw new AdminApiException($"Endpoint not found at {resolvedUrl}{path}. Verify the Admin API version matches the CLI version.");
            case >= 500:
                throw new AdminApiException($"Admin API server error: {statusCode}. (URL: {resolvedUrl})");
        }

        _ = response.EnsureSuccessStatusCode();

        string json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return JsonSerializer.Deserialize<T>(json, JsonDefaults.Options)
                ?? throw new AdminApiException("Invalid response from Admin API. Possible version mismatch between CLI and server.");
        }
        catch (JsonException ex)
        {
            throw new AdminApiException("Invalid response from Admin API. Possible version mismatch between CLI and server.", ex);
        }
    }

    /// <summary>
    /// Sends a GET request and deserializes the JSON response, returning null on HTTP 404.
    /// All other HTTP errors are handled identically to <see cref="GetAsync{T}"/>.
    /// </summary>
    /// <exception cref="AdminApiException">Thrown for all API communication errors except 404.</exception>
    public async Task<T?> TryGetAsync<T>(string path, CancellationToken cancellationToken)
        where T : class
    {
        string resolvedUrl = _httpClient.BaseAddress?.ToString().TrimEnd('/') ?? "unknown";
        HttpResponseMessage response;
        try
        {
            response = await _httpClient.GetAsync(path, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex) when (ex.InnerException is System.Net.Sockets.SocketException)
        {
            throw new AdminApiException($"Cannot connect to Admin API at {resolvedUrl}. Is the server running?", ex);
        }
        catch (HttpRequestException ex)
        {
            throw new AdminApiException($"Cannot connect to Admin API at {resolvedUrl}. Is the server running?", ex);
        }
        catch (TaskCanceledException ex)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw new AdminApiException($"Request was canceled. (URL: {resolvedUrl})", ex);
            }

            throw new AdminApiException($"Request timed out after 10 seconds. (URL: {resolvedUrl})", ex);
        }

        using (response)
        {
            int statusCode = (int)response.StatusCode;
            switch (statusCode)
            {
                case 401:
                    throw new AdminApiException($"Authentication required. Use --token to provide a JWT token. (URL: {resolvedUrl})");
                case 403:
                    throw new AdminApiException($"Access denied. Insufficient permissions. (URL: {resolvedUrl})");
                case 404:
                    return null;
                case >= 500:
                    throw new AdminApiException($"Admin API server error: {statusCode}. (URL: {resolvedUrl})");
            }

            _ = response.EnsureSuccessStatusCode();

            string json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                return JsonSerializer.Deserialize<T>(json, JsonDefaults.Options)
                    ?? throw new AdminApiException("Invalid response from Admin API. Possible version mismatch between CLI and server.");
            }
            catch (JsonException ex)
            {
                throw new AdminApiException("Invalid response from Admin API. Possible version mismatch between CLI and server.", ex);
            }
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_ownsClient)
        {
            _httpClient.Dispose();
        }

        GC.SuppressFinalize(this);
    }
}
