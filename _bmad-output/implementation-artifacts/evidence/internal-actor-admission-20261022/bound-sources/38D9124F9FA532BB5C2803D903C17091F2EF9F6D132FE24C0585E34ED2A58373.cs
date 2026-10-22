using System.Text.Json;

namespace Hexalith.EventStore.Client.Projections;

public sealed partial class DaprReadModelStore
{
    /// <inheritdoc/>
    public async Task<ReadModelEntry<TValue>> GetCompactedAsync<TValue>(string storeName, string key,
        CancellationToken cancellationToken = default) where TValue : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storeName);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var accessor = new DaprReadModelBatchStateAccessor(_daprClient, storeName);
        RawStateEntry entry = await ReadModelBatchProtocol.ReadCompactedAsync(accessor, key,
            cancellationToken, _options.TimeProvider).ConfigureAwait(false);
        return entry.Exists
            ? new(JsonSerializer.Deserialize<TValue>(entry.Value.Span, _daprClient.JsonSerializerOptions), entry.ETag) { ExpiresAtUtc = entry.ExpiresAtUtc }
            : new(null, null) { ExpiresAtUtc = entry.ExpiresAtUtc };
    }
}
