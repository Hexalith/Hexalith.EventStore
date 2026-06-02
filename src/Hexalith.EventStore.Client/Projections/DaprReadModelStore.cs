using Dapr.Client;

namespace Hexalith.EventStore.Client.Projections;

/// <summary>
/// DAPR state-store implementation of <see cref="IReadModelStore"/>.
/// </summary>
/// <remarks>
/// Writes via <see cref="TrySaveAsync{TValue}"/> use first-write-wins concurrency so the
/// reload-and-merge loop in <see cref="ReadModelWritePolicy"/> can detect and retry on conflict.
/// </remarks>
/// <param name="daprClient">The DAPR client used to access the state store.</param>
public sealed class DaprReadModelStore(DaprClient daprClient) : IReadModelStore {
    /// <inheritdoc/>
    public async Task<ReadModelEntry<TValue>> GetAsync<TValue>(
        string storeName,
        string key,
        CancellationToken cancellationToken = default)
        where TValue : class {
        ArgumentException.ThrowIfNullOrWhiteSpace(storeName);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        (TValue value, string etag) = await daprClient
            .GetStateAndETagAsync<TValue>(storeName, key, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return new ReadModelEntry<TValue>(value, etag);
    }

    /// <inheritdoc/>
    public async Task SaveAsync<TValue>(
        string storeName,
        string key,
        TValue value,
        CancellationToken cancellationToken = default)
        where TValue : class {
        ArgumentException.ThrowIfNullOrWhiteSpace(storeName);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);

        await daprClient
            .SaveStateAsync(storeName, key, value, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<bool> TrySaveAsync<TValue>(
        string storeName,
        string key,
        TValue value,
        string etag,
        CancellationToken cancellationToken = default)
        where TValue : class {
        ArgumentException.ThrowIfNullOrWhiteSpace(storeName);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(etag);

        return await daprClient
            .TrySaveStateAsync(
                storeName,
                key,
                value,
                etag,
                new StateOptions { Concurrency = ConcurrencyMode.FirstWrite },
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }
}
