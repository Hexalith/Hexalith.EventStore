using Hexalith.EventStore.Contracts.Projections;

namespace Hexalith.EventStore.DomainService;

/// <summary>
/// Opts an epoch handler into durable per-owner rebuild partitions instead of one tenant-wide candidate.
/// Both folds are side-effect-free; the SDK persists each partition before accepting its inventory ordinal.
/// </summary>
public interface IAsyncDomainSharedProjectionPartitionedRebuildHandler : IAsyncDomainSharedProjectionEpochHandler
{
    /// <summary>Produces one bounded, immutable owner partition from a complete non-erased history.</summary>
    Task<ReadOnlyMemory<byte>> PreparePartitionAsync(
        DomainSharedProjectionRebuildIdentity identity,
        ProjectionRequest aggregateHistory,
        CancellationToken cancellationToken);

    /// <summary>
    /// Produces bounded chunks for one owner. Handlers with potentially large owner histories override
    /// this method; existing single-partition handlers inherit the compatible one-chunk behavior.
    /// </summary>
    async Task<IReadOnlyList<ReadOnlyMemory<byte>>> PreparePartitionChunksAsync(
        DomainSharedProjectionRebuildIdentity identity,
        ProjectionRequest aggregateHistory,
        CancellationToken cancellationToken)
        => [await PreparePartitionAsync(identity, aggregateHistory, cancellationToken).ConfigureAwait(false)];

    /// <summary>Combines durable partitions into the complete replacement plan for one tenant.</summary>
    Task<DomainProjectionRebuildPlan> FinalizePartitionsAsync(
        DomainSharedProjectionRebuildIdentity identity,
        IReadOnlyList<DomainSharedProjectionRebuildPartition> partitions,
        CancellationToken cancellationToken);
}
