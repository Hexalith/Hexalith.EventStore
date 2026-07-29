namespace Hexalith.EventStore.Client.Projections;

/// <summary>
/// Additive read-model persistence capability for values whose durable identity is intentionally bounded.
/// </summary>
/// <remarks>
/// The ordinary <see cref="IReadModelStore"/> contract remains unchanged. Projection handlers use this
/// companion only for ephemeral coordination records, such as delivery ledgers, and keep durable read models
/// on the non-expiring surface.
/// </remarks>
public interface IReadModelExpiringStore {
    /// <summary>Attempts an optimistic-concurrency write with a state-store time-to-live.</summary>
    /// <typeparam name="TValue">The read-model value type.</typeparam>
    /// <param name="storeName">The DAPR state-store component name.</param>
    /// <param name="key">The state key.</param>
    /// <param name="value">The value to persist.</param>
    /// <param name="etag">The expected ETag, or empty for create-only.</param>
    /// <param name="timeToLive">The positive retention period, rounded up to whole seconds.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns><see langword="true"/> when the write succeeded; otherwise <see langword="false"/>.</returns>
    Task<bool> TrySaveWithTimeToLiveAsync<TValue>(
        string storeName,
        string key,
        TValue value,
        string etag,
        TimeSpan timeToLive,
        CancellationToken cancellationToken = default)
        where TValue : class;
}
