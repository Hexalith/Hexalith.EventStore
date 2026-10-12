namespace Hexalith.EventStore.DomainService;

/// <summary>Enumerates operation-scoped private keys that a streamed rebuild created outside owner partitions.</summary>
public interface IAsyncDomainSharedProjectionScratchCleanupHandler : IAsyncDomainSharedProjectionStreamingRebuildHandler
{
    /// <summary>Streams private keys to erase after commit or abort while owner chunks remain readable.</summary>
    IAsyncEnumerable<string> EnumerateScratchKeysAsync(
        DomainSharedProjectionRebuildIdentity identity,
        IDomainSharedProjectionPartitionCursor partitions,
        CancellationToken cancellationToken);
}
