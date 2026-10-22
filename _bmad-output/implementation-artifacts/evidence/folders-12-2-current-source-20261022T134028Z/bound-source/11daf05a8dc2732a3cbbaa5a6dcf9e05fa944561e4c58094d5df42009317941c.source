using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;

using Hexalith.EventStore.Client.Projections;

namespace Hexalith.EventStore.DomainService;

/// <summary>Reads accepted owner partitions and writes operation-scoped scratch under the epoch fence.</summary>
/// <param name="store">The read-model store containing durable owner partitions.</param>
/// <param name="storeName">The store used for scratch batch receipts.</param>
/// <param name="state">The current durable rebuild session.</param>
/// <param name="coordinator">The optional epoch coordinator for scratch writes.</param>
/// <param name="scope">The optional epoch scope for scratch writes.</param>
internal sealed class DomainSharedProjectionPartitionCursor(
    IReadModelStore store,
    string storeName,
    DomainSharedProjectionRebuildSessionState state,
    SharedProjectionEpochCoordinator? coordinator = null,
    SharedProjectionScope? scope = null) : IDomainSharedProjectionPartitionCursor
{
    private readonly string _sessionKey = DomainSharedProjectionRebuildSessionKey.Compute(state.Identity);

    /// <inheritdoc />
    public Task<bool> TryWriteScratchAsync<TValue>(string key, TValue value, string batchId,
        ReadModelBatchConcurrency concurrency, CancellationToken cancellationToken) where TValue : class
        => (coordinator ?? throw new InvalidOperationException("The partition cursor has no scratch writer."))
            .TryWriteScratchAsync(scope ?? throw new InvalidOperationException("The partition cursor has no epoch scope."),
            state.Identity.OperationId, key, value, batchId,
            concurrency, cancellationToken);

    /// <inheritdoc />
    public string ScratchMarkerKey(string batchId)
        => new ReadModelBatchScope(storeName, state.Identity.TenantId, state.Identity.Domain,
            state.Identity.ProjectionType, state.Identity.ProjectionType, batchId).ComputeMarkerKey();

    /// <inheritdoc />
    public async IAsyncEnumerable<DomainSharedProjectionRebuildPartition> ReadAllAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        string fingerprint = DomainSharedProjectionRebuildFingerprint.EmptyInventory;
        string? previousOwner = null;
        for (long ordinal = 0; ordinal < state.AcceptedAggregateCount; ordinal++)
        {
            DomainSharedProjectionStoredPartitionManifest manifest = await ReadManifestAsync(ordinal, cancellationToken)
                .ConfigureAwait(false);
            if (previousOwner is not null && string.CompareOrdinal(previousOwner, manifest.AggregateId) >= 0)
            {
                throw new InvalidOperationException("The streamed owner inventory is out of order.");
            }

            previousOwner = manifest.AggregateId;
            fingerprint = DomainSharedProjectionRebuildFingerprint.AppendInventory(
                fingerprint, ordinal, manifest.HistoryFingerprint);
            for (int chunk = 0; chunk < manifest.MaterializedChunkCount; chunk++)
            {
                yield return await ReadChunkAsync(manifest, chunk, cancellationToken).ConfigureAwait(false);
            }
        }

        if (!string.Equals(fingerprint, state.InventoryFingerprint, StringComparison.Ordinal)
            || !string.Equals(previousOwner, state.LastAggregateId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The streamed owner inventory conflicts with the accepted fingerprint.");
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DomainSharedProjectionRebuildPartition>> ReadRangeAsync(
        long ownerOrdinal,
        int firstChunkOrdinal,
        int chunkCount,
        CancellationToken cancellationToken)
    {
        if (ownerOrdinal < 0 || ownerOrdinal >= state.AcceptedAggregateCount
            || firstChunkOrdinal < 0 || chunkCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(firstChunkOrdinal));
        }

        DomainSharedProjectionStoredPartitionManifest manifest = await ReadManifestAsync(ownerOrdinal, cancellationToken)
            .ConfigureAwait(false);
        if (chunkCount > manifest.MaterializedChunkCount - firstChunkOrdinal)
        {
            throw new InvalidOperationException("The requested owner chunk range exceeds its manifest.");
        }

        var chunks = new List<DomainSharedProjectionRebuildPartition>(chunkCount);
        for (int ordinal = firstChunkOrdinal; ordinal < firstChunkOrdinal + chunkCount; ordinal++)
        {
            chunks.Add(await ReadChunkAsync(manifest, ordinal, cancellationToken).ConfigureAwait(false));
        }

        return chunks;
    }

    private async Task<DomainSharedProjectionStoredPartitionManifest> ReadManifestAsync(
        long ordinal, CancellationToken cancellationToken)
    {
        ReadModelEntry<DomainSharedProjectionStoredPartitionManifest> entry = await store
            .GetAsync<DomainSharedProjectionStoredPartitionManifest>(storeName,
                DomainSharedProjectionRebuildDispatcher.PartitionManifestKey(_sessionKey, ordinal), cancellationToken)
            .ConfigureAwait(false);
        DomainSharedProjectionStoredPartitionManifest manifest = entry.Value
            ?? throw new InvalidOperationException("A streamed owner manifest is missing.");
        if (manifest.Ordinal != ordinal || string.IsNullOrWhiteSpace(manifest.AggregateId)
            || string.IsNullOrWhiteSpace(manifest.HistoryFingerprint)
            || manifest.ChunkHashes is null
            || manifest.StreamedChunkCount < 0
            || manifest.IsErased && manifest.MaterializedChunkCount != 0
            || !manifest.IsErased && manifest.MaterializedChunkCount == 0
            || manifest.StreamedChunkCount > 0 &&
                (manifest.ChunkHashes.Length != 0 || manifest.ChunkHashPageDigests is null
                 || manifest.ChunkHashPageDigests.Length !=
                    ((long)manifest.StreamedChunkCount + DomainSharedProjectionRebuildDispatcher.ChunkHashesPerPage - 1)
                    / DomainSharedProjectionRebuildDispatcher.ChunkHashesPerPage))
        {
            throw new InvalidOperationException("A streamed owner manifest is invalid.");
        }

        return manifest;
    }

    private async Task<DomainSharedProjectionRebuildPartition> ReadChunkAsync(
        DomainSharedProjectionStoredPartitionManifest manifest,
        int chunkOrdinal,
        CancellationToken cancellationToken)
    {
        string expectedHash = await ExpectedChunkHashAsync(manifest, chunkOrdinal, cancellationToken)
            .ConfigureAwait(false);
        ReadModelEntry<DomainSharedProjectionStoredPartition> entry = await store
            .GetAsync<DomainSharedProjectionStoredPartition>(storeName,
                DomainSharedProjectionRebuildDispatcher.PartitionChunkKey(_sessionKey, manifest.Ordinal, chunkOrdinal),
                cancellationToken).ConfigureAwait(false);
        DomainSharedProjectionStoredPartition chunk = entry.Value
            ?? throw new InvalidOperationException("A streamed owner chunk is missing.");
        if (chunk.Ordinal != manifest.Ordinal || chunk.ChunkOrdinal != chunkOrdinal
            || !string.Equals(chunk.AggregateId, manifest.AggregateId, StringComparison.Ordinal)
            || !string.Equals(chunk.HistoryFingerprint, manifest.HistoryFingerprint, StringComparison.Ordinal)
            || chunk.State is null || chunk.State.Length == 0
            || !string.Equals(Convert.ToHexString(SHA256.HashData(chunk.State)),
                expectedHash, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("A streamed owner chunk conflicts with its manifest.");
        }

        return new DomainSharedProjectionRebuildPartition(manifest.Ordinal, manifest.AggregateId,
            chunk.State, chunkOrdinal);
    }

    private async Task<string> ExpectedChunkHashAsync(
        DomainSharedProjectionStoredPartitionManifest manifest, int chunkOrdinal,
        CancellationToken cancellationToken)
    {
        if (manifest.StreamedChunkCount == 0) return manifest.ChunkHashes[chunkOrdinal];
        int pageOrdinal = chunkOrdinal / DomainSharedProjectionRebuildDispatcher.ChunkHashesPerPage;
        DomainSharedProjectionChunkHashPage page = (await store
            .GetAsync<DomainSharedProjectionChunkHashPage>(storeName,
                DomainSharedProjectionRebuildDispatcher.PartitionHashPageKey(_sessionKey,
                    manifest.Ordinal, pageOrdinal), cancellationToken).ConfigureAwait(false)).Value
            ?? throw new InvalidOperationException("A streamed owner hash page is missing.");
        if (page.OwnerOrdinal != manifest.Ordinal || page.AggregateId != manifest.AggregateId
            || page.PageOrdinal != pageOrdinal || page.Hashes is null
            || page.Hashes.Length != Math.Min(DomainSharedProjectionRebuildDispatcher.ChunkHashesPerPage,
                manifest.StreamedChunkCount - (long)pageOrdinal * DomainSharedProjectionRebuildDispatcher.ChunkHashesPerPage)
            || !Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(page)))
                .Equals(manifest.ChunkHashPageDigests![pageOrdinal], StringComparison.Ordinal))
            throw new InvalidOperationException("A streamed owner hash page conflicts with its manifest.");
        return page.Hashes[chunkOrdinal % DomainSharedProjectionRebuildDispatcher.ChunkHashesPerPage];
    }
}
