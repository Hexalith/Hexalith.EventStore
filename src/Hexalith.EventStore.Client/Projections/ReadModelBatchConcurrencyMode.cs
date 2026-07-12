namespace Hexalith.EventStore.Client.Projections;

/// <summary>
/// The optimistic-concurrency mode of a single read-model batch operation.
/// </summary>
public enum ReadModelBatchConcurrencyMode {
    /// <summary>
    /// Unconditional last-write. Valid for writes only; the value is written regardless of the current
    /// stored value or ETag.
    /// </summary>
    Unconditional,

    /// <summary>
    /// First-write against an expected ETag. An empty expected ETag means create-only for a write (the
    /// key must be absent); a non-empty expected ETag means the current stored ETag must match. Deletes
    /// require a non-empty expected ETag.
    /// </summary>
    ExpectedETag,

    /// <summary>
    /// Idempotent-absent delete. Valid for deletes only; the key is removed if present and treated as a
    /// success if already absent, without requiring an ETag.
    /// </summary>
    IdempotentAbsent,
}
