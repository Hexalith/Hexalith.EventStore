namespace Hexalith.EventStore.Client.Projections;

/// <summary>
/// Reads a bounded set of read-model keys through one store-level bulk operation.
/// </summary>
/// <remarks>
/// This additive seam keeps domain list projections from issuing one remote state-store request per row.
/// Callers remain responsible for page/chunk bounds and for validating every requested key in the result.
/// </remarks>
public interface IReadModelBulkStore {
    /// <summary>Reads the requested keys and returns one entry per key in request order.</summary>
    /// <typeparam name="TValue">The read-model type.</typeparam>
    /// <param name="storeName">The DAPR state-store component name.</param>
    /// <param name="keys">The distinct state keys to read.</param>
    /// <param name="parallelism">The maximum parallelism delegated to the backing store.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>One keyed entry per requested key, including absent values.</returns>
    Task<IReadOnlyList<ReadModelBulkEntry<TValue>>> GetManyAsync<TValue>(
        string storeName,
        IReadOnlyList<string> keys,
        int parallelism,
        CancellationToken cancellationToken = default)
        where TValue : class;
}

/// <summary>A bulk read-model value paired with its state key and ETag.</summary>
/// <typeparam name="TValue">The read-model type.</typeparam>
/// <param name="Key">The requested state key.</param>
/// <param name="Value">The persisted value, or <see langword="null"/> when absent.</param>
/// <param name="ETag">The ETag of the read, or <see langword="null"/> when absent.</param>
public sealed record ReadModelBulkEntry<TValue>(string Key, TValue? Value, string? ETag)
    where TValue : class;
