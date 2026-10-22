using Hexalith.EventStore.Contracts.Projections;

namespace Hexalith.EventStore.DomainService;

/// <summary>Folds a bounded transport-part stream without assembling the owner's payload history.</summary>
public interface IAsyncDomainSharedProjectionStreamedOwnerHandler : IAsyncDomainSharedProjectionStreamingRebuildHandler
{
    /// <summary>Yields deterministic bounded owner chunks in ordinal order.</summary>
    IAsyncEnumerable<ReadOnlyMemory<byte>> StreamPartitionChunksAsync(
        DomainSharedProjectionRebuildIdentity identity,
        string aggregateId,
        IAsyncEnumerable<ProjectionEventDto> events,
        CancellationToken cancellationToken);
}
