using System.Text.Json;

using Hexalith.EventStore.Client.Gateway;

namespace Hexalith.EventStore.Sample.BlazorUI.Services;

/// <summary>
/// Result of a counter status query, including the current count and optional ETag for caching.
/// </summary>
/// <param name="Count">The current counter value.</param>
/// <param name="ETag">The ETag from the query response, used for HTTP 304 caching.</param>
public sealed record CounterStatusResult(int Count, string? ETag)
{
    /// <summary>
    /// Gets the empty counter state used before a projection exists.
    /// </summary>
    public static CounterStatusResult Empty { get; } = new(0, null);

    /// <summary>
    /// Creates a component state result from a gateway query result.
    /// </summary>
    public static CounterStatusResult FromQueryResult(EventStoreQueryResult result, CounterStatusResult? cachedResult)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.IsNotModified)
        {
            return cachedResult ?? new CounterStatusResult(0, result.ETag);
        }

        int count = result.Payload.HasValue
            ? ParseCountFromPayload(result.Payload.Value)
            : 0;

        return new CounterStatusResult(count, result.ETag);
    }

    private static int ParseCountFromPayload(JsonElement payloadElement)
    {
        // Payload may be a base64-encoded JSON object or a direct JSON object.
        if (payloadElement.ValueKind == JsonValueKind.String)
        {
            string? base64 = payloadElement.GetString();
            if (base64 is not null)
            {
                byte[] bytes = Convert.FromBase64String(base64);
                using var inner = JsonDocument.Parse(bytes);
                if (inner.RootElement.TryGetProperty("count", out JsonElement countEl))
                {
                    return countEl.GetInt32();
                }
            }
        }
        else if (payloadElement.ValueKind == JsonValueKind.Object
            && payloadElement.TryGetProperty("count", out JsonElement directCount))
        {
            return directCount.GetInt32();
        }

        return 0;
    }
}
