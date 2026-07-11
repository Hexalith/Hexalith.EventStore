namespace Hexalith.EventStore.Client.Projections;

/// <summary>
/// Persisted read-model store for domain modules that maintain incrementally-updated,
/// multi-key read models in a DAPR state store.
/// </summary>
/// <remarks>
/// <para>
/// This is the platform generalization of the per-domain projection state stores domain modules
/// previously hand-wrote (e.g. <c>DaprTenantProjectionStateStore</c>). It is a thin wrapper over the
/// DAPR state-store API exposing only what a persisted read model needs: ETag-aware reads and
/// optimistic-concurrency writes. The higher-level reload-and-merge retry loop lives in
/// <see cref="ReadModelWritePolicy"/>, which is built on top of this interface so it can be unit-tested
/// against an in-memory store without a DAPR sidecar.
/// </para>
/// <para>
/// Multi-key/index read models are supported by simply addressing different <c>key</c> values: a
/// per-aggregate read model uses a key derived from the aggregate identity, while a cross-aggregate
/// index uses a single fixed key whose value is merged on every write via
/// <see cref="ReadModelWritePolicy"/>.
/// </para>
/// </remarks>
public interface IReadModelStore {
    /// <summary>
    /// Reads a read-model value together with its current ETag.
    /// </summary>
    /// <typeparam name="TValue">The read-model type.</typeparam>
    /// <param name="storeName">The DAPR state-store component name.</param>
    /// <param name="key">The state key.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The value (or <see langword="null"/> when absent) and its ETag.</returns>
    Task<ReadModelEntry<TValue>> GetAsync<TValue>(
        string storeName,
        string key,
        CancellationToken cancellationToken = default)
        where TValue : class;

    /// <summary>
    /// Saves a read-model value unconditionally (last-write-wins).
    /// </summary>
    /// <typeparam name="TValue">The read-model type.</typeparam>
    /// <param name="storeName">The DAPR state-store component name.</param>
    /// <param name="key">The state key.</param>
    /// <param name="value">The value to persist.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task representing the save operation.</returns>
    Task SaveAsync<TValue>(
        string storeName,
        string key,
        TValue value,
        CancellationToken cancellationToken = default)
        where TValue : class;

    /// <summary>
    /// Attempts to save a read-model value under optimistic concurrency (first-write-wins).
    /// </summary>
    /// <typeparam name="TValue">The read-model type.</typeparam>
    /// <param name="storeName">The DAPR state-store component name.</param>
    /// <param name="key">The state key.</param>
    /// <param name="value">The value to persist.</param>
    /// <param name="etag">The expected ETag (empty string for a first insert).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns><see langword="true"/> when the write succeeded; <see langword="false"/> on ETag conflict.</returns>
    Task<bool> TrySaveAsync<TValue>(
        string storeName,
        string key,
        TValue value,
        string etag,
        CancellationToken cancellationToken = default)
        where TValue : class;

    /// <summary>
    /// Attempts to erase a read-model value under optimistic concurrency (first-write-wins).
    /// </summary>
    /// <param name="storeName">The DAPR state-store component name.</param>
    /// <param name="key">The state key.</param>
    /// <param name="etag">The expected ETag.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>
    /// <see langword="true"/> when the value was erased or was already absent;
    /// <see langword="false"/> when a present value has a different ETag.
    /// </returns>
    Task<bool> TryEraseAsync(
        string storeName,
        string key,
        string etag,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// A read-model value paired with the ETag under which it was read.
/// </summary>
/// <typeparam name="TValue">The read-model type.</typeparam>
/// <param name="Value">The persisted value, or <see langword="null"/> when the key is absent.</param>
/// <param name="ETag">The ETag of the read, or <see langword="null"/> when the key is absent.</param>
public sealed record ReadModelEntry<TValue>(TValue? Value, string? ETag)
    where TValue : class;
