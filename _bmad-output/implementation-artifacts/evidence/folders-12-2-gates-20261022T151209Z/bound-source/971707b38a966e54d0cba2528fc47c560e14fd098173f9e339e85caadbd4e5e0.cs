using Hexalith.EventStore.Client.Projections;

namespace Hexalith.EventStore.DomainService;

/// <summary>Opts a partitioned epoch handler into bounded, replayable final-operation streaming.</summary>
/// <remarks>
/// The handler may write deterministic, operation-scoped private indexes while enumerating. These
/// writes must be idempotent on retry and must never be reader-visible. Only the coordinator's
/// sealed staged generation publishes final operations.
/// </remarks>
public interface IAsyncDomainSharedProjectionStreamingRebuildHandler : IAsyncDomainSharedProjectionPartitionedRebuildHandler
{
    /// <summary>
    /// Produces deterministic logical writes. The cursor may be read more than once; the implementation
    /// must reject contradictory contributions and yield each logical key exactly once.
    /// </summary>
    IAsyncEnumerable<ReadModelBatchOperation> StreamFinalizedOperationsAsync(
        DomainSharedProjectionRebuildIdentity identity,
        IDomainSharedProjectionPartitionCursor partitions,
        CancellationToken cancellationToken);
}
