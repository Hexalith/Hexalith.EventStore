namespace Hexalith.EventStore.Client.Projections;

/// <summary>
/// Deployment-tunable options and per-store execution profiles for coordinated read-model batches.
/// </summary>
/// <remarks>
/// The defaults are the versioned contract; a deployment may lower a limit but changing a default or the
/// fingerprint material is a versioned contract change with compatibility tests. Stores absent from
/// <see cref="StoreProfiles"/> default to <see cref="ReadModelBatchStoreProfile.Resumable"/>.
/// </remarks>
public sealed class ReadModelBatchOptions {
    /// <summary>The default maximum number of operations in one batch.</summary>
    public const int DefaultMaxOperations = 100;

    /// <summary>The default maximum total canonical manifest byte size.</summary>
    public const int DefaultMaxCanonicalManifestBytes = 1_048_576;

    /// <summary>The default maximum UTF-8 byte length of a single logical key.</summary>
    public const int DefaultMaxKeyByteLength = 512;

    /// <summary>Gets or sets the maximum number of operations allowed in one batch (default 100).</summary>
    public int MaxOperations { get; set; } = DefaultMaxOperations;

    /// <summary>Gets or sets the maximum total canonical manifest byte size (default 1,048,576).</summary>
    public int MaxCanonicalManifestBytes { get; set; } = DefaultMaxCanonicalManifestBytes;

    /// <summary>Gets or sets the maximum UTF-8 byte length of a single logical key (default 512).</summary>
    public int MaxKeyByteLength { get; set; } = DefaultMaxKeyByteLength;

    /// <summary>
    /// Gets or sets the bounded, caller-token-independent timeout used for durable reconciliation after an
    /// ambiguous dispatch (default 30 seconds).
    /// </summary>
    public TimeSpan ReconciliationTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets the per-store execution profiles, keyed by DAPR state-store component name (ordinal).
    /// </summary>
    public Dictionary<string, ReadModelBatchStoreProfile> StoreProfiles { get; } =
        new(StringComparer.Ordinal);

    /// <summary>
    /// Resolves the execution profile for a store, defaulting to
    /// <see cref="ReadModelBatchStoreProfile.Resumable"/> when the store is not explicitly qualified.
    /// </summary>
    /// <param name="storeName">The DAPR state-store component name.</param>
    /// <returns>The configured profile, or <see cref="ReadModelBatchStoreProfile.Resumable"/>.</returns>
    public ReadModelBatchStoreProfile GetProfile(string storeName) {
        ArgumentException.ThrowIfNullOrWhiteSpace(storeName);
        return StoreProfiles.TryGetValue(storeName, out ReadModelBatchStoreProfile profile)
            ? profile
            : ReadModelBatchStoreProfile.Resumable;
    }

    /// <summary>
    /// Validates that all limits are positive and the reconciliation timeout is greater than zero.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">A limit or the timeout is not positive.</exception>
    public void Validate() {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaxOperations);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaxCanonicalManifestBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaxKeyByteLength);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(ReconciliationTimeout, TimeSpan.Zero);
    }
}
