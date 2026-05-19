using System.Net.Http.Headers;
using System.Text.Json;

using Hexalith.EventStore.Admin.Abstractions.Security;
using Hexalith.EventStore.Admin.Cli.Formatting;
using Hexalith.EventStore.Contracts.Problems;

namespace Hexalith.EventStore.Admin.Cli.Client;

/// <summary>
/// HTTP client wrapper for calling the Admin REST API.
/// </summary>
public class AdminApiClient : IDisposable {
    private const int MaxProblemDetailsBytes = 65_536;
    private const int MaxExtensionValueChars = 1024;
    private readonly HttpClient _httpClient;
    private readonly bool _ownsClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdminApiClient"/> class with options from global CLI options.
    /// </summary>
    public AdminApiClient(GlobalOptions options) {
        ArgumentNullException.ThrowIfNull(options);
        _httpClient = new HttpClient {
            BaseAddress = new Uri(options.Url),
            Timeout = TimeSpan.FromSeconds(10),
        };
        if (options.Token is not null) {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.Token);
        }

        _ownsClient = true;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AdminApiClient"/> class with options and a custom handler (for testing).
    /// </summary>
    internal AdminApiClient(GlobalOptions options, HttpMessageHandler handler) {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(handler);

        _httpClient = new HttpClient(handler) {
            BaseAddress = new Uri(options.Url),
            Timeout = TimeSpan.FromSeconds(10),
        };

        if (options.Token is not null) {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.Token);
        }

        _ownsClient = true;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AdminApiClient"/> class with a pre-configured HTTP client (for testing).
    /// </summary>
    internal AdminApiClient(HttpClient httpClient) {
        _httpClient = httpClient;
        _ownsClient = false;
    }

    /// <summary>
    /// Sends a GET request and deserializes the JSON response.
    /// </summary>
    /// <exception cref="AdminApiException">Thrown for all API communication errors with user-friendly messages.</exception>
    public async Task<T> GetAsync<T>(string path, CancellationToken cancellationToken) {
        string resolvedUrl = _httpClient.BaseAddress?.ToString().TrimEnd('/') ?? "unknown";
        HttpResponseMessage response;
        try {
            response = await _httpClient.GetAsync(path, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex) when (ex.InnerException is System.Net.Sockets.SocketException) {
            throw new AdminApiException($"Cannot connect to Admin API at {resolvedUrl}. Is the server running?", ex);
        }
        catch (HttpRequestException ex) {
            throw new AdminApiException($"Cannot connect to Admin API at {resolvedUrl}. Is the server running?", ex);
        }
        catch (TaskCanceledException ex) {
            if (cancellationToken.IsCancellationRequested) {
                throw new AdminApiException($"Request was canceled. (URL: {resolvedUrl})", ex);
            }

            throw new AdminApiException($"Request timed out after 10 seconds. (URL: {resolvedUrl})", ex);
        }

        using (response) {
            AdminApiException? problemException = await TryCreateProblemExceptionAsync(response, path, resolvedUrl, cancellationToken).ConfigureAwait(false);
            if (problemException is not null) {
                throw problemException;
            }

            int statusCode = (int)response.StatusCode;
            switch (statusCode) {
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
            try {
                return JsonSerializer.Deserialize<T>(json, JsonDefaults.Options)
                    ?? throw new AdminApiException("Invalid response from Admin API. Possible version mismatch between CLI and server.");
            }
            catch (JsonException ex) {
                throw new AdminApiException("Invalid response from Admin API. Possible version mismatch between CLI and server.", ex);
            }
        }
    }

    /// <summary>
    /// Sends a GET request and deserializes the JSON response, returning null on HTTP 404.
    /// All other HTTP errors are handled identically to <see cref="GetAsync{T}"/>.
    /// </summary>
    /// <exception cref="AdminApiException">Thrown for all API communication errors except 404.</exception>
    public async Task<T?> TryGetAsync<T>(string path, CancellationToken cancellationToken)
        where T : class {
        string resolvedUrl = _httpClient.BaseAddress?.ToString().TrimEnd('/') ?? "unknown";
        HttpResponseMessage response;
        try {
            response = await _httpClient.GetAsync(path, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex) when (ex.InnerException is System.Net.Sockets.SocketException) {
            throw new AdminApiException($"Cannot connect to Admin API at {resolvedUrl}. Is the server running?", ex);
        }
        catch (HttpRequestException ex) {
            throw new AdminApiException($"Cannot connect to Admin API at {resolvedUrl}. Is the server running?", ex);
        }
        catch (TaskCanceledException ex) {
            if (cancellationToken.IsCancellationRequested) {
                throw new AdminApiException($"Request was canceled. (URL: {resolvedUrl})", ex);
            }

            throw new AdminApiException($"Request timed out after 10 seconds. (URL: {resolvedUrl})", ex);
        }

        using (response) {
            int statusCode = (int)response.StatusCode;
            if (statusCode == 404) {
                return null;
            }

            AdminApiException? problemException = await TryCreateProblemExceptionAsync(response, path, resolvedUrl, cancellationToken).ConfigureAwait(false);
            if (problemException is not null) {
                throw problemException;
            }

            switch (statusCode) {
                case 401:
                    throw new AdminApiException($"Authentication required. Use --token to provide a JWT token. (URL: {resolvedUrl})");
                case 403:
                    throw new AdminApiException($"Access denied. Insufficient permissions. (URL: {resolvedUrl})");
                case >= 500:
                    throw new AdminApiException($"Admin API server error: {statusCode}. (URL: {resolvedUrl})");
            }

            _ = response.EnsureSuccessStatusCode();

            string json = await ReadResponseBodyAsync(response, resolvedUrl, cancellationToken).ConfigureAwait(false);
            try {
                return JsonSerializer.Deserialize<T>(json, JsonDefaults.Options)
                    ?? throw new AdminApiException("Invalid response from Admin API. Possible version mismatch between CLI and server.");
            }
            catch (JsonException ex) {
                throw new AdminApiException("Invalid response from Admin API. Possible version mismatch between CLI and server.", ex);
            }
        }
    }

    /// <summary>
    /// Sends a POST request with an optional JSON body and deserializes the JSON response.
    /// Treats both HTTP 200 and 202 as success.
    /// </summary>
    /// <exception cref="AdminApiException">Thrown for all API communication errors with user-friendly messages.</exception>
    public async Task<TResponse> PostAsync<TResponse>(string path, object? body, CancellationToken cancellationToken) {
        string resolvedUrl = _httpClient.BaseAddress?.ToString().TrimEnd('/') ?? "unknown";
        HttpResponseMessage response;
        try {
            StringContent content = new(
                body is not null
                    ? JsonSerializer.Serialize(body, JsonDefaults.Options)
                    : "{}",
                System.Text.Encoding.UTF8,
                "application/json");
            response = await _httpClient.PostAsync(path, content, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex) when (ex.InnerException is System.Net.Sockets.SocketException) {
            throw new AdminApiException($"Cannot connect to Admin API at {resolvedUrl}. Is the server running?", ex);
        }
        catch (HttpRequestException ex) {
            throw new AdminApiException($"Cannot connect to Admin API at {resolvedUrl}. Is the server running?", ex);
        }
        catch (TaskCanceledException ex) {
            if (cancellationToken.IsCancellationRequested) {
                throw new AdminApiException($"Request was canceled. (URL: {resolvedUrl})", ex);
            }

            throw new AdminApiException($"Request timed out after 10 seconds. (URL: {resolvedUrl})", ex);
        }

        using (response) {
            AdminApiException? problemException = await TryCreateProblemExceptionAsync(response, path, resolvedUrl, cancellationToken).ConfigureAwait(false);
            if (problemException is not null) {
                throw problemException;
            }

            int statusCode = (int)response.StatusCode;
            switch (statusCode) {
                case 401:
                    throw new AdminApiException($"Authentication required. Use --token to provide a JWT token. (URL: {resolvedUrl})", 401);
                case 403:
                    throw new AdminApiException($"Access denied. Insufficient permissions. (URL: {resolvedUrl})", 403);
                case 404:
                    throw new AdminApiException($"Resource not found at {resolvedUrl}{path}.", 404);
                case >= 500:
                    throw new AdminApiException($"Admin API server error: {statusCode}. (URL: {resolvedUrl})", statusCode);
            }

            // Treat both 200 and 202 as success
            if (statusCode is not (200 or 202)) {
                _ = response.EnsureSuccessStatusCode();
            }

            string json = await ReadResponseBodyAsync(response, resolvedUrl, cancellationToken).ConfigureAwait(false);
            try {
                return JsonSerializer.Deserialize<TResponse>(json, JsonDefaults.Options)
                    ?? throw new AdminApiException("Invalid response from Admin API. Possible version mismatch between CLI and server.");
            }
            catch (JsonException ex) {
                throw new AdminApiException("Invalid response from Admin API. Possible version mismatch between CLI and server.", ex);
            }
        }
    }

    /// <summary>
    /// Sends a POST request with no body and deserializes the JSON response.
    /// </summary>
    /// <exception cref="AdminApiException">Thrown for all API communication errors with user-friendly messages.</exception>
    public async Task<TResponse> PostAsync<TResponse>(string path, CancellationToken cancellationToken)
        => await PostAsync<TResponse>(path, null, cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Sends a PUT request with no body and deserializes the JSON response.
    /// Treats both HTTP 200 and 202 as success.
    /// </summary>
    /// <exception cref="AdminApiException">Thrown for all API communication errors.</exception>
    public async Task<TResponse> PutAsync<TResponse>(string path, CancellationToken cancellationToken) {
        string resolvedUrl = _httpClient.BaseAddress?.ToString().TrimEnd('/') ?? "unknown";
        HttpResponseMessage response;
        try {
            response = await _httpClient.PutAsync(path, null, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex) when (ex.InnerException is System.Net.Sockets.SocketException) {
            throw new AdminApiException($"Cannot connect to Admin API at {resolvedUrl}. Is the server running?", ex);
        }
        catch (HttpRequestException ex) {
            throw new AdminApiException($"Cannot connect to Admin API at {resolvedUrl}. Is the server running?", ex);
        }
        catch (TaskCanceledException ex) {
            if (cancellationToken.IsCancellationRequested) {
                throw new AdminApiException($"Request was canceled. (URL: {resolvedUrl})", ex);
            }

            throw new AdminApiException($"Request timed out after 10 seconds. (URL: {resolvedUrl})", ex);
        }

        using (response) {
            AdminApiException? problemException = await TryCreateProblemExceptionAsync(response, path, resolvedUrl, cancellationToken).ConfigureAwait(false);
            if (problemException is not null) {
                throw problemException;
            }

            int statusCode = (int)response.StatusCode;
            switch (statusCode) {
                case 401:
                    throw new AdminApiException($"Authentication required. Use --token to provide a JWT token. (URL: {resolvedUrl})", 401);
                case 403:
                    throw new AdminApiException($"Access denied. Insufficient permissions. (URL: {resolvedUrl})", 403);
                case 404:
                    throw new AdminApiException($"Resource not found at {resolvedUrl}{path}.", 404);
                case >= 500:
                    throw new AdminApiException($"Admin API server error: {statusCode}. (URL: {resolvedUrl})", statusCode);
            }

            if (statusCode is not (200 or 202)) {
                _ = response.EnsureSuccessStatusCode();
            }

            string json = await ReadResponseBodyAsync(response, resolvedUrl, cancellationToken).ConfigureAwait(false);
            try {
                return JsonSerializer.Deserialize<TResponse>(json, JsonDefaults.Options)
                    ?? throw new AdminApiException("Invalid response from Admin API. Possible version mismatch between CLI and server.");
            }
            catch (JsonException ex) {
                throw new AdminApiException("Invalid response from Admin API. Possible version mismatch between CLI and server.", ex);
            }
        }
    }

    /// <summary>
    /// Sends a DELETE request and deserializes the JSON response.
    /// Treats both HTTP 200 and 202 as success.
    /// </summary>
    /// <exception cref="AdminApiException">Thrown for all API communication errors.</exception>
    public async Task<TResponse> DeleteAsync<TResponse>(string path, CancellationToken cancellationToken) {
        string resolvedUrl = _httpClient.BaseAddress?.ToString().TrimEnd('/') ?? "unknown";
        HttpResponseMessage response;
        try {
            response = await _httpClient.DeleteAsync(path, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex) when (ex.InnerException is System.Net.Sockets.SocketException) {
            throw new AdminApiException($"Cannot connect to Admin API at {resolvedUrl}. Is the server running?", ex);
        }
        catch (HttpRequestException ex) {
            throw new AdminApiException($"Cannot connect to Admin API at {resolvedUrl}. Is the server running?", ex);
        }
        catch (TaskCanceledException ex) {
            if (cancellationToken.IsCancellationRequested) {
                throw new AdminApiException($"Request was canceled. (URL: {resolvedUrl})", ex);
            }

            throw new AdminApiException($"Request timed out after 10 seconds. (URL: {resolvedUrl})", ex);
        }

        using (response) {
            AdminApiException? problemException = await TryCreateProblemExceptionAsync(response, path, resolvedUrl, cancellationToken).ConfigureAwait(false);
            if (problemException is not null) {
                throw problemException;
            }

            int statusCode = (int)response.StatusCode;
            switch (statusCode) {
                case 401:
                    throw new AdminApiException($"Authentication required. Use --token to provide a JWT token. (URL: {resolvedUrl})", 401);
                case 403:
                    throw new AdminApiException($"Access denied. Insufficient permissions. (URL: {resolvedUrl})", 403);
                case 404:
                    throw new AdminApiException($"Resource not found at {resolvedUrl}{path}.", 404);
                case >= 500:
                    throw new AdminApiException($"Admin API server error: {statusCode}. (URL: {resolvedUrl})", statusCode);
            }

            if (statusCode is not (200 or 202)) {
                _ = response.EnsureSuccessStatusCode();
            }

            string json = await ReadResponseBodyAsync(response, resolvedUrl, cancellationToken).ConfigureAwait(false);
            try {
                return JsonSerializer.Deserialize<TResponse>(json, JsonDefaults.Options)
                    ?? throw new AdminApiException("Invalid response from Admin API. Possible version mismatch between CLI and server.");
            }
            catch (JsonException ex) {
                throw new AdminApiException("Invalid response from Admin API. Possible version mismatch between CLI and server.", ex);
            }
        }
    }

    /// <inheritdoc/>
    public void Dispose() {
        if (_ownsClient) {
            _httpClient.Dispose();
        }

        GC.SuppressFinalize(this);
    }

    private static async Task<AdminApiException?> TryCreateProblemExceptionAsync(
        HttpResponseMessage response,
        string path,
        string resolvedUrl,
        CancellationToken cancellationToken) {
        if (response.IsSuccessStatusCode) {
            return null;
        }

        int statusCode = (int)response.StatusCode;
        long? contentLength = response.Content.Headers.ContentLength;
        if (contentLength is 0) {
            return null;
        }

        if (contentLength > MaxProblemDetailsBytes) {
            return CreateGenericFailure(statusCode, resolvedUrl, path);
        }

        byte[]? body;
        try {
            body = await ReadBoundedBytesAsync(response, MaxProblemDetailsBytes, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException) {
            return CreateGenericFailure(statusCode, resolvedUrl, path);
        }
        catch (IOException) {
            return CreateGenericFailure(statusCode, resolvedUrl, path);
        }

        if (body is null) {
            return CreateGenericFailure(statusCode, resolvedUrl, path);
        }

        if (body.Length == 0) {
            return null;
        }

        try {
            using JsonDocument doc = JsonDocument.Parse(body);
            if (doc.RootElement.ValueKind is not JsonValueKind.Object) {
                return CreateGenericFailure(statusCode, resolvedUrl, path);
            }

            AdminApiProblemDetails problem = ParseSafeProblemDetails(doc.RootElement, statusCode);
            if (problem.Type is null && problem.Title is null && problem.Extensions.Count == 0) {
                return CreateGenericFailure(statusCode, resolvedUrl, path);
            }

            return new AdminApiException(BuildProblemMessage(problem, statusCode), statusCode, problem);
        }
        catch (JsonException) {
            return CreateGenericFailure(statusCode, resolvedUrl, path);
        }
    }

    /// <summary>
    /// Reads up to <paramref name="maxBytes"/> bytes from the response body.
    /// Returns <see langword="null"/> when the body exceeds the cap (size enforced before full buffering).
    /// </summary>
    private static async Task<byte[]?> ReadBoundedBytesAsync(
        HttpResponseMessage response,
        int maxBytes,
        CancellationToken cancellationToken) {
        using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using MemoryStream buffer = new();
        byte[] chunk = new byte[8192];
        int total = 0;
        while (true) {
            int read = await stream.ReadAsync(chunk.AsMemory(0, chunk.Length), cancellationToken).ConfigureAwait(false);
            if (read == 0) {
                break;
            }

            total += read;
            if (total > maxBytes) {
                return null;
            }

            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }

        return buffer.ToArray();
    }

    /// <summary>
    /// Reads the full response body as UTF-8 text, wrapping transport/IO exceptions in
    /// a user-friendly <see cref="AdminApiException"/>.
    /// </summary>
    private static async Task<string> ReadResponseBodyAsync(
        HttpResponseMessage response,
        string resolvedUrl,
        CancellationToken cancellationToken) {
        try {
            return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex) {
            throw new AdminApiException($"Cannot read Admin API response. (URL: {resolvedUrl})", ex);
        }
        catch (IOException ex) {
            throw new AdminApiException($"Cannot read Admin API response. (URL: {resolvedUrl})", ex);
        }
    }

    private static AdminApiException CreateGenericFailure(int statusCode, string resolvedUrl, string path)
        => new($"Admin API request failed with HTTP {statusCode}. (URL: {resolvedUrl}{path})", statusCode);

    private static AdminApiProblemDetails ParseSafeProblemDetails(JsonElement root, int fallbackStatusCode) {
        string? type = GetSafeString(root, "type");
        string? title = GetSafeString(root, "title");
        string? detail = GetSafeString(root, "detail");
        int? status = root.TryGetProperty("status", out JsonElement statusElement) && statusElement.TryGetInt32(out int parsedStatus)
            ? parsedStatus
            : fallbackStatusCode;

        Dictionary<string, string> extensions = new(StringComparer.Ordinal);
        foreach (JsonProperty property in root.EnumerateObject()) {
            if (IsStandardProblemProperty(property.Name) || !IsAllowedProblemExtension(property.Name)) {
                continue;
            }

            if (TryGetSafeScalar(property.Value, out string? safeValue) && safeValue is not null) {
                extensions[property.Name] = safeValue;
            }
        }

        return new AdminApiProblemDetails(type, title, status, detail, extensions);
    }

    private static string BuildProblemMessage(AdminApiProblemDetails problem, int statusCode) {
        string title = string.IsNullOrWhiteSpace(problem.Title)
            ? "Admin API request failed"
            : problem.Title;

        return problem.Extensions.TryGetValue(GatewayProblemDetailsExtensions.ReasonCode, out string? reasonCode)
            ? $"{title}. HTTP {statusCode}. Reason: {reasonCode}."
            : $"{title}. HTTP {statusCode}.";
    }

    private static string? GetSafeString(JsonElement root, string propertyName)
        => root.TryGetProperty(propertyName, out JsonElement value) && TryGetSafeScalar(value, out string? safeValue)
            ? safeValue
            : null;

    private static bool TryGetSafeScalar(JsonElement value, out string? safeValue) {
        safeValue = null;
        switch (value.ValueKind) {
            case JsonValueKind.String:
                string? text = value.GetString();
                if (string.IsNullOrEmpty(text) || UnsafeMarkerDetection.ContainsUnsafeMarker(text)) {
                    return false;
                }

                safeValue = text.Length > MaxExtensionValueChars
                    ? string.Concat(text.AsSpan(0, MaxExtensionValueChars - 1), "…")
                    : text;
                return true;
            case JsonValueKind.Number:
                safeValue = value.GetRawText();
                return true;
            case JsonValueKind.True:
                safeValue = "true";
                return true;
            case JsonValueKind.False:
                safeValue = "false";
                return true;
            default:
                return false;
        }
    }

    private static bool IsStandardProblemProperty(string propertyName)
        => propertyName.Equals("type", StringComparison.Ordinal)
        || propertyName.Equals("title", StringComparison.Ordinal)
        || propertyName.Equals("status", StringComparison.Ordinal)
        || propertyName.Equals("detail", StringComparison.Ordinal)
        || propertyName.Equals("instance", StringComparison.Ordinal);

    private static bool IsAllowedProblemExtension(string propertyName)
        => propertyName.Equals(GatewayProblemDetailsExtensions.CorrelationId, StringComparison.Ordinal)
        || propertyName.Equals(GatewayProblemDetailsExtensions.TenantId, StringComparison.Ordinal)
        || propertyName.Equals(GatewayProblemDetailsExtensions.Reason, StringComparison.Ordinal)
        || propertyName.Equals(GatewayProblemDetailsExtensions.ReasonCode, StringComparison.Ordinal)
        || propertyName.Equals(GatewayProblemDetailsExtensions.RetryAfter, StringComparison.Ordinal)
        || propertyName.Equals(UnreadableProtectedDataProblem.ExtensionReasonCategory, StringComparison.Ordinal)
        || propertyName.Equals(UnreadableProtectedDataProblem.ExtensionStage, StringComparison.Ordinal)
        || propertyName.Equals(UnreadableProtectedDataProblem.ExtensionSequenceNumber, StringComparison.Ordinal)
        || propertyName.Equals(UnreadableProtectedDataProblem.ExtensionCheckpointId, StringComparison.Ordinal)
        || propertyName.Equals(UnreadableProtectedDataProblem.ExtensionDomain, StringComparison.Ordinal)
        || propertyName.Equals(UnreadableProtectedDataProblem.ExtensionAggregateId, StringComparison.Ordinal)
        || propertyName.Equals(UnreadableProtectedDataProblem.ExtensionMetadataVersion, StringComparison.Ordinal)
        || propertyName.Equals(UnreadableProtectedDataProblem.ExtensionRetryable, StringComparison.Ordinal)
        || propertyName.Equals(UnreadableProtectedDataProblem.ExtensionPermanent, StringComparison.Ordinal);
}
