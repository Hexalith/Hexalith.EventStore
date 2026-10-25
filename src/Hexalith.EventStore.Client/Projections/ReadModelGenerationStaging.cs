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
        int bytes = 0, ordinal = 0;
        foreach (ReadModelBatchOperation operation in operations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ArgumentNullException.ThrowIfNull(operation);
            if (!keys.Add(operation.Key)) throw new ArgumentException("Generation keys must be globally unique.", nameof(operations));
            if (operation.Kind != ReadModelBatchOperationKind.Write
                || operation.Concurrency.Mode != ReadModelBatchConcurrencyMode.ExpectedETag
                || operation.Concurrency.ExpectedETag.Length != 0 || operation.TimeToLive is not null || operation.ExpiresAtUtc is not null)
            {
                throw new ArgumentException("Generation staging requires immutable nonexpiring create-only values.", nameof(operations));
            }
            int size = operation.CanonicalValue.Length + System.Text.Encoding.UTF8.GetByteCount(operation.Key) * 6 + 2048;
            if (size > 512 * 1024)
            {
                throw new ArgumentException("A generation value exceeds its bounded group.", nameof(operations));
            }
            if (group.Count == 32 || bytes + size > 512 * 1024)
            {
                AddGroup();
            }
            group.Add(operation);
            bytes += size;
        }
        if (group.Count != 0)
        {
            AddGroup();
        }
        foreach (ReadModelBatch batch in batches)
        {
            ReadModelBatchResult result = await store.ExecuteAsync(batch, cancellationToken).ConfigureAwait(false);
            if (!result.IsSuccess) throw new ReadModelGenerationStagingException(result);
        }
        void AddGroup()
        {
            var batch = new ReadModelBatch(scope with
            {
                ProjectionType = scope.ProjectionType + ".generation",
                BatchId = "generation-" + scope.ComputeScopeHash() + "-" + ordinal++
            }, group)
            {
                RetainManifestForRecovery = true
            };
            ReadModelBatchValidation.Validate(batch, new ReadModelBatchOptions());
            batches.Add(batch);
            group = [];
            bytes = 0;
        }
    }
}
