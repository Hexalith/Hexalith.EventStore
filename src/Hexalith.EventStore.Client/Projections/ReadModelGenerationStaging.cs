namespace Hexalith.EventStore.Client.Projections;

/// <summary>Persists disjoint immutable generation values in resumable bounded groups before their authority pointer is published.</summary>
public static class ReadModelGenerationStaging
{
    /// <summary>Stages create-only generation operations. Repeated calls resume identical SDK batches; no canonical pointer is changed.</summary>
    /// <param name="store">The owning SDK batch store.</param>
    /// <param name="scope">The stable aggregate and operation identity.</param>
    /// <param name="operations">The disjoint immutable generation writes.</param>
    /// <param name="cancellationToken">Cancels bounded staging work.</param>
    public static async Task StageAsync(IReadModelBatchStore store, ReadModelBatchScope scope,
        IEnumerable<ReadModelBatchOperation> operations, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(operations);
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
            if (ReadModelBatchFingerprint.BuildCanonicalManifest(CreateGroup()).Length > 512 * 1024)
            {
                group.RemoveAt(group.Count - 1);
                if (group.Count != 0) AddGroup();
                group.Add(operation);
                if (ReadModelBatchFingerprint.BuildCanonicalManifest(CreateGroup()).Length > 512 * 1024)
                    throw new ArgumentException("A generation value exceeds its bounded group.", nameof(operations));
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
