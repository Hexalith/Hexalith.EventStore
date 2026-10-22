namespace Hexalith.EventStore.Client.Projections;

/// <summary>
/// The optimistic-concurrency policy applied to a single read-model batch operation.
/// </summary>
/// <remarks>
/// A missing ETag is never silently translated into last-write behavior. Writes are either
/// <see cref="LastWrite"/> (unconditional), <see cref="CreateOnly"/> (write only if the key is absent),
/// or <see cref="Match(string)"/> (write only if the current ETag matches). Deletes are either
/// <see cref="Match(string)"/> against an existing non-empty ETag or <see cref="IdempotentAbsent"/>.
/// </remarks>
/// <param name="Mode">The concurrency mode.</param>
/// <param name="ExpectedETag">
/// The expected ETag. Empty for <see cref="ReadModelBatchConcurrencyMode.Unconditional"/>,
/// <see cref="ReadModelBatchConcurrencyMode.IdempotentAbsent"/>, and create-only writes.
/// </param>
public sealed record ReadModelBatchConcurrency(ReadModelBatchConcurrencyMode Mode, string ExpectedETag) {
    /// <summary>Gets the unconditional last-write policy (valid for writes only).</summary>
    public static ReadModelBatchConcurrency LastWrite { get; } =
        new(ReadModelBatchConcurrencyMode.Unconditional, string.Empty);

    /// <summary>Gets the create-only policy (write succeeds only if the key is absent).</summary>
    public static ReadModelBatchConcurrency CreateOnly { get; } =
        new(ReadModelBatchConcurrencyMode.ExpectedETag, string.Empty);

    /// <summary>Gets the idempotent-absent delete policy (delete if present, success if absent).</summary>
    public static ReadModelBatchConcurrency IdempotentAbsent { get; } =
        new(ReadModelBatchConcurrencyMode.IdempotentAbsent, string.Empty);

    /// <summary>
    /// Creates a first-write policy against a specific non-empty expected ETag.
    /// </summary>
    /// <param name="etag">The expected current ETag (must be non-empty).</param>
    /// <returns>An expected-ETag concurrency policy.</returns>
    /// <exception cref="ArgumentException">The ETag is null or empty.</exception>
    public static ReadModelBatchConcurrency Match(string etag) {
        ArgumentException.ThrowIfNullOrEmpty(etag);
        return new ReadModelBatchConcurrency(ReadModelBatchConcurrencyMode.ExpectedETag, etag);
    }
}
