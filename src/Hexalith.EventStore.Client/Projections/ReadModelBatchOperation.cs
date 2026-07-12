namespace Hexalith.EventStore.Client.Projections;

/// <summary>
/// A single immutable operation within a coordinated read-model batch.
/// </summary>
/// <remarks>
/// Write operations serialize their value once, at construction, into immutable canonical UTF-8 JSON
/// bytes. Callers therefore cannot mutate a payload after the fact in a way that would change the batch
/// fingerprint. Deletes carry no value. The logical <see cref="Key"/> and the serialized
/// <see cref="CanonicalValue"/> participate in the batch fingerprint together with the ordinal position,
/// <see cref="Kind"/>, <see cref="ValueTypeName"/>, and <see cref="Concurrency"/> policy.
/// </remarks>
public sealed class ReadModelBatchOperation {
    private ReadModelBatchOperation(
        string key,
        ReadModelBatchOperationKind kind,
        string valueTypeName,
        ReadOnlyMemory<byte> canonicalValue,
        ReadModelBatchConcurrency concurrency) {
        Key = key;
        Kind = kind;
        ValueTypeName = valueTypeName;
        CanonicalValue = canonicalValue;
        Concurrency = concurrency;
    }

    /// <summary>Gets the logical state key the operation targets.</summary>
    public string Key { get; }

    /// <summary>Gets the operation kind (write or delete).</summary>
    public ReadModelBatchOperationKind Kind { get; }

    /// <summary>Gets the stable serialized value type identity (empty for deletes).</summary>
    public string ValueTypeName { get; }

    /// <summary>Gets the canonical UTF-8 JSON bytes written to the store (empty for deletes).</summary>
    public ReadOnlyMemory<byte> CanonicalValue { get; }

    /// <summary>Gets the optimistic-concurrency policy for the operation.</summary>
    public ReadModelBatchConcurrency Concurrency { get; }

    /// <summary>
    /// Creates a write operation, serializing <paramref name="value"/> immediately into immutable
    /// canonical bytes.
    /// </summary>
    /// <typeparam name="TValue">The read-model value type.</typeparam>
    /// <param name="key">The logical state key.</param>
    /// <param name="value">The value to persist.</param>
    /// <param name="concurrency">
    /// The concurrency policy. Must be <see cref="ReadModelBatchConcurrencyMode.Unconditional"/> or
    /// <see cref="ReadModelBatchConcurrencyMode.ExpectedETag"/>; idempotent-absent is delete-only.
    /// </param>
    /// <returns>An immutable write operation.</returns>
    /// <exception cref="ArgumentException">The key is null/whitespace or the concurrency mode is invalid for a write.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> or <paramref name="concurrency"/> is null.</exception>
    public static ReadModelBatchOperation Write<TValue>(
        string key,
        TValue value,
        ReadModelBatchConcurrency concurrency)
        where TValue : class {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(concurrency);
        if (concurrency.Mode == ReadModelBatchConcurrencyMode.IdempotentAbsent) {
            throw new ArgumentException(
                "Idempotent-absent concurrency is only valid for delete operations.",
                nameof(concurrency));
        }

        ReadOnlyMemory<byte> canonical = ReadModelBatchCanonicalJson.Serialize(value);
        string typeName = typeof(TValue).FullName ?? typeof(TValue).Name;
        return new ReadModelBatchOperation(
            key,
            ReadModelBatchOperationKind.Write,
            typeName,
            canonical,
            concurrency);
    }

    /// <summary>
    /// Creates a delete operation.
    /// </summary>
    /// <param name="key">The logical state key.</param>
    /// <param name="concurrency">
    /// The concurrency policy. Must be a non-empty <see cref="ReadModelBatchConcurrencyMode.ExpectedETag"/>
    /// or <see cref="ReadModelBatchConcurrencyMode.IdempotentAbsent"/>.
    /// </param>
    /// <returns>An immutable delete operation.</returns>
    /// <exception cref="ArgumentException">The key is null/whitespace or the concurrency mode is invalid for a delete.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="concurrency"/> is null.</exception>
    public static ReadModelBatchOperation Delete(string key, ReadModelBatchConcurrency concurrency) {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(concurrency);
        bool valid = concurrency.Mode switch {
            ReadModelBatchConcurrencyMode.IdempotentAbsent => true,
            ReadModelBatchConcurrencyMode.ExpectedETag => concurrency.ExpectedETag.Length > 0,
            _ => false,
        };
        if (!valid) {
            throw new ArgumentException(
                "Delete operations require a non-empty expected ETag or the idempotent-absent policy.",
                nameof(concurrency));
        }

        return new ReadModelBatchOperation(
            key,
            ReadModelBatchOperationKind.Delete,
            string.Empty,
            ReadOnlyMemory<byte>.Empty,
            concurrency);
    }
}
