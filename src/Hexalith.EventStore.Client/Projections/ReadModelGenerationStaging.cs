namespace Hexalith.EventStore.Client.Projections;

/// <summary>Persists disjoint immutable generation values in resumable bounded groups before their authority pointer is published.</summary>
public static class ReadModelGenerationStaging
{
    /// <summary>Stages create-only generation operations. Repeated calls resume identical SDK batches; no canonical pointer is changed.</summary>
    /// <param name="store">The owning SDK batch store.</param>
    /// <param name="scope">The stable aggregate and operation identity.</param>
    /// <param name="operations">The disjoint immutable generation writes.</param>
    /// <param name="cancellationToken">Cancels bounded staging work.</param>
    public static Task StageAsync(IReadModelBatchStore store, ReadModelBatchScope scope,
        IEnumerable<ReadModelBatchOperation> operations, CancellationToken cancellationToken = default)
        => StageAsync(store, scope, operations, 512 * 1024, cancellationToken);

    /// <summary>Stages immutable values using an explicit finite canonical-byte budget and at most 32 operations per group.</summary>
    /// <param name="store">The owning SDK batch store; its effective limits are preflighted for every group.</param>
    /// <param name="scope">The stable aggregate and operation identity.</param>
    /// <param name="operations">The disjoint immutable generation writes.</param>
    /// <param name="maxCanonicalManifestBytes">The complete encoded manifest budget, at most the SDK hard recovery ceiling of 1 MiB.</param>
    /// <param name="cancellationToken">Cancels bounded staging work.</param>
    public static async Task StageAsync(IReadModelBatchStore store, ReadModelBatchScope scope,
        IEnumerable<ReadModelBatchOperation> operations, int maxCanonicalManifestBytes, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxCanonicalManifestBytes, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(maxCanonicalManifestBytes, ReadModelBatchOptions.DefaultMaxCanonicalManifestBytes);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(operations);
        // Preserve the existing conservative partition identities whenever every value fits.
        // An explicit larger ceiling admits only an oversized individual immutable value;
        // it never repacks previously resumable 512-KiB groups into different manifests.
        int groupBudget = Math.Min(512 * 1024, maxCanonicalManifestBytes);
        var batches = new List<ReadModelBatch>();
        var keys = new HashSet<string>(StringComparer.Ordinal);
        var group = new List<ReadModelBatchOperation>();
        int ordinal = 0;
        foreach (ReadModelBatchOperation operation in operations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ArgumentNullException.ThrowIfNull(operation);
            if (!keys.Add(operation.Key)) throw new ArgumentException("Generation keys must be globally unique.", nameof(operations));
            if (operation.Kind != ReadModelBatchOperationKind.Write
                || operation.Concurrency.Mode != ReadModelBatchConcurrencyMode.ExpectedETag
                || operation.Concurrency.ExpectedETag is not { Length: 0 } || operation.TimeToLive is not null || operation.ExpiresAtUtc is not null)
            {
                throw new ArgumentException("Generation staging requires immutable nonexpiring create-only values.", nameof(operations));
            }
            if (group.Count == 32)
            {
                AddGroup();
            }
            group.Add(operation);
            if (ReadModelBatchFingerprint.BuildCanonicalManifest(CreateGroup()).Length > groupBudget)
            {
                group.RemoveAt(group.Count - 1);
                if (group.Count != 0) AddGroup();
                group.Add(operation);
                int singleBytes = ReadModelBatchFingerprint.BuildCanonicalManifest(CreateGroup()).Length;
                if (singleBytes > maxCanonicalManifestBytes)
                    throw new ArgumentException("A generation value exceeds its bounded group.", nameof(operations));
                if (singleBytes > groupBudget) AddGroup();
            }
        }
        if (group.Count != 0)
        {
            AddGroup();
        }
        // Every group, including late keys and values, must pass the actual store's limits before
        // the first durable write. Canonical measurement includes scope and base64-encoded values.
        foreach (ReadModelBatch batch in batches)
        {
            ReadModelBatchValidation.Validate(batch, new ReadModelBatchOptions());
            store.ValidateBatch(batch);
        }
        foreach (ReadModelBatch batch in batches)
        {
            ReadModelBatchResult result = await store.ExecuteAsync(batch, cancellationToken).ConfigureAwait(false);
            if (!result.IsSuccess) throw new ReadModelGenerationStagingException(result);
        }
        ReadModelBatch CreateGroup() => new(scope with
        {
            ProjectionType = System.Text.Encoding.UTF8.GetByteCount(scope.ProjectionType + ".generation") <= ReadModelBatchScope.MaxComponentByteLength
                ? scope.ProjectionType + ".generation" : "generation-" + scope.ComputeScopeHash(),
            BatchId = "generation-" + scope.ComputeScopeHash() + "-" + ordinal
        }, group)
        {
            RetainManifestForRecovery = true
        };
        void AddGroup()
        {
            batches.Add(CreateGroup());
            ordinal++;
            group = [];
        }
    }
}
