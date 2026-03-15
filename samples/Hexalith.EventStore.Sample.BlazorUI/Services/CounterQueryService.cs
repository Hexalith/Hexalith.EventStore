using System.Net;
using System.Text.Json;

namespace Hexalith.EventStore.Sample.BlazorUI.Services;

/// <summary>
/// Result of a counter status query, including the current count and optional ETag for caching.
/// </summary>
/// <param name="Count">The current counter value.</param>
/// <param name="ETag">The ETag from the query response, used for HTTP 304 caching.</param>
public sealed record CounterStatusResult(int Count, string? ETag);

/// <summary>
/// Queries the CommandApi for counter projection status.
/// Handles ETag-based caching (HTTP 304) to minimize unnecessary data transfer.
/// </summary>
public sealed class CounterQueryService(IHttpClientFactory httpClientFactory) {
    private static readonly JsonElement _emptyPayload = JsonDocument.Parse("{}").RootElement.Clone();
    private CounterStatusResult? _cachedResult;
    private string? _lastETag;

    /// <summary>
    /// Gets the current counter status from the query endpoint.
    /// Uses If-None-Match header with cached ETag for HTTP 304 optimization.
    /// </summary>
    public async Task<CounterStatusResult> GetCounterStatusAsync(string tenantId, CancellationToken ct = default) {
        HttpClient client = httpClientFactory.CreateClient("EventStoreApi");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/queries");
        request.Content = JsonContent.Create(new {
            domain = "counter",
            tenant = tenantId,
            aggregateId = "counter-1",
            queryType = "get-counter-status",
            payload = _emptyPayload,
            entityId = (string?)null,
        });

        if (_lastETag is not null) {
            _ = request.Headers.TryAddWithoutValidation("If-None-Match", $"\"{_lastETag}\"");
        }

        using HttpResponseMessage response = await client.SendAsync(request, ct).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotModified && _cachedResult is not null) {
            return _cachedResult;
        }

        // 404 means no projection exists yet (no commands submitted) — return zero count.
        if (response.StatusCode == HttpStatusCode.NotFound) {
            return new CounterStatusResult(0, null);
        }

        _ = response.EnsureSuccessStatusCode();

        string? eTag = response.Headers.ETag?.Tag?.Trim('"');
        JsonDocument? body = await response.Content.ReadFromJsonAsync<JsonDocument>(ct).ConfigureAwait(false);
        int count = body?.RootElement.TryGetProperty("payload", out JsonElement payloadEl) == true
            ? ParseCountFromPayload(payloadEl)
            : 0;

        _cachedResult = new CounterStatusResult(count, eTag);
        _lastETag = eTag;
        return _cachedResult;
    }

    private static int ParseCountFromPayload(JsonElement payloadElement) {
        // Payload may be a base64-encoded JSON object or a direct JSON object
        if (payloadElement.ValueKind == JsonValueKind.String) {
            string? base64 = payloadElement.GetString();
            if (base64 is not null) {
                byte[] bytes = Convert.FromBase64String(base64);
                using var inner = JsonDocument.Parse(bytes);
                if (inner.RootElement.TryGetProperty("count", out JsonElement countEl)) {
                    return countEl.GetInt32();
                }
            }
        }
        else if (payloadElement.ValueKind == JsonValueKind.Object
            && payloadElement.TryGetProperty("count", out JsonElement directCount)) {
            return directCount.GetInt32();
        }

        return 0;
    }
}
