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
    /// <summary>Describes an unusable response without exposing its contents.</summary>
    internal const string InvalidPayloadMessage = "The counter projection returned an invalid count.";

    /// <summary>
    /// Gets the empty counter state used before a projection exists.
    /// </summary>
    public static CounterStatusResult Empty { get; } = new(0, null);

    /// <summary>
    /// Creates a component state result from a gateway query result.
    /// </summary>
    /// <param name="result">The gateway response.</param>
    /// <param name="cachedResult">The last successfully read state, required to interpret an HTTP 304.</param>
    /// <returns>The validated state, or the latest valid fallback (<see cref="Empty"/> when none) for an unusable response.</returns>
    public static CounterStatusResult FromQueryResult(EventStoreQueryResult result, CounterStatusResult? cachedResult)
    {
        _ = TryFromQueryResult(result, cachedResult, out CounterStatusResult state);
        return state;
    }

    /// <summary>Reports whether the gateway supplied a valid projection or an unchanged response.</summary>
    /// <param name="result">The gateway response.</param>
    /// <param name="cachedResult">The last successfully read state.</param>
    /// <param name="state">The validated state, or the latest valid fallback when the response is unusable.</param>
    /// <returns>Whether the response can be recorded as a successful refresh.</returns>
    internal static bool TryFromQueryResult(
        EventStoreQueryResult result, CounterStatusResult? cachedResult, out CounterStatusResult state)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.IsNotModified)
        {
            // A 304 only confirms the state whose validator the caller sent. Without that state, or for another
            // validator, there is no confirmed count, so report the response as unusable instead of pairing an
            // invented count with the validator; a caller holding no validator then reads without If-None-Match.
            state = cachedResult ?? Empty;
            return cachedResult is { ETag: not null }
                && string.Equals(cachedResult.ETag, result.ETag, StringComparison.Ordinal);
        }

        if (result.Payload is { } payload && ParseCountFromPayload(payload) is int count)
        {
            state = new CounterStatusResult(count, result.ETag);
            return true;
        }

        // Keep the last valid state and validator. The next refresh can retry the bad response.
        state = cachedResult ?? Empty;
        return false;
    }

    private static int? ParseCountFromPayload(JsonElement payloadElement)
    {
        try
        {
            if (payloadElement.ValueKind != JsonValueKind.String)
            {
                return ReadCount(payloadElement);
            }

            using var inner = JsonDocument.Parse(Convert.FromBase64String(payloadElement.GetString()!));
            return ReadCount(inner.RootElement);
        }
        catch (FormatException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            // JsonDocument can retain a string containing invalid Unicode that GetString cannot decode.
            return null;
        }
    }

    private static int? ReadCount(JsonElement payload)
        => payload.ValueKind == JsonValueKind.Object
            && payload.TryGetProperty("count", out JsonElement count)
            && count.ValueKind == JsonValueKind.Number
            && count.TryGetInt32(out int value)
            ? value
            : null;
}
