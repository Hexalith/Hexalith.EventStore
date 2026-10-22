namespace Hexalith.EventStore.DomainService;

using Hexalith.EventStore.Client.Projections;

/// <summary>Reads verified, durable owner chunks without materializing a tenant-wide candidate.</summary>
public interface IDomainSharedProjectionPartitionCursor
{
    /// <summary>Streams every accepted owner chunk in inventory order.</summary>
    IAsyncEnumerable<DomainSharedProjectionRebuildPartition> ReadAllAsync(CancellationToken cancellationToken);

    /// <summary>Reads a bounded consecutive range from one owner for deterministic cross-owner joins.</summary>
    Task<IReadOnlyList<DomainSharedProjectionRebuildPartition>> ReadRangeAsync(
        long ownerOrdinal,
        int firstChunkOrdinal,
        int chunkCount,
        CancellationToken cancellationToken);

    /// <summary>Conditionally writes private scratch while its rebuild epoch still owns the fence.</summary>
    Task<bool> TryWriteScratchAsync<TValue>(string key, TValue value, string batchId,
        ReadModelBatchConcurrency concurrency, CancellationToken cancellationToken) where TValue : class;

    /// <summary>Gets the cleanup key for a scratch write batch receipt.</summary>
    string ScratchMarkerKey(string batchId);
}
