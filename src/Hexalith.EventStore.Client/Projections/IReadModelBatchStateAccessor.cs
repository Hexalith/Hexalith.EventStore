namespace Hexalith.EventStore.Client.Projections;

/// <summary>
/// The minimal raw byte-state surface the coordinated batch protocol runs over. Implemented once by the
/// DAPR adapter (real sidecar) and once by the in-memory fake so both expose equivalent observable batch
/// outcomes.
/// </summary>
internal interface IReadModelBatchStateAccessor {
    /// <summary>Gets a value indicating whether the backing store can execute a single ordered state transaction.</summary>
    bool SupportsTransaction { get; }

    /// <summary>Reads a key's raw value and ETag.</summary>
    /// <param name="key">The state key.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The raw state entry.</returns>
    Task<RawStateEntry> ReadAsync(string key, CancellationToken cancellationToken);

    /// <summary>
    /// Writes a value under first-write optimistic concurrency. An empty expected ETag means create-only
    /// (succeeds only when the key is absent).
    /// </summary>
    /// <param name="key">The state key.</param>
    /// <param name="value">The raw value bytes.</param>
    /// <param name="expectedETag">The expected ETag, or empty for create-only.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns><see langword="true"/> when the write succeeded; <see langword="false"/> on ETag conflict.</returns>
    Task<bool> TryWriteAsync(string key, ReadOnlyMemory<byte> value, string expectedETag, CancellationToken cancellationToken);

    /// <summary>Deletes a value under first-write optimistic concurrency.</summary>
    /// <param name="key">The state key.</param>
    /// <param name="expectedETag">The expected ETag.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns><see langword="true"/> when the delete succeeded; <see langword="false"/> on ETag conflict.</returns>
    Task<bool> TryDeleteAsync(string key, string expectedETag, CancellationToken cancellationToken);

    /// <summary>
    /// Executes a single ordered state transaction. Only called when <see cref="SupportsTransaction"/> is
    /// <see langword="true"/>.
    /// </summary>
    /// <param name="operations">The ordered operations.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task representing the transaction.</returns>
    Task ExecuteTransactionAsync(IReadOnlyList<RawTransactionOperation> operations, CancellationToken cancellationToken);
}
