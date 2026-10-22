using Hexalith.EventStore.Contracts.Projections;

namespace Hexalith.EventStore.DomainService;

/// <summary>
/// Opts a named projection into side-effect-free tenant/domain shared rebuild accumulation.
/// </summary>
/// <remarks>
/// Every method must be deterministic and free of read-model mutations because optimistic session retries
/// may invoke it more than once. Finalization must describe the complete desired shared view, including
/// writes or deletes required to prune stale entries. Erased aggregate histories are excluded by the SDK
/// before <see cref="AccumulateAsync"/> is invoked.
/// </remarks>
public interface IAsyncDomainSharedProjectionRebuildHandler : IAsyncDomainProjectionHandler {
    /// <summary>Gets the state-store component used by both the private session and finalized batch.</summary>
    string RebuildStoreName { get; }

    /// <summary>Creates the empty operation-scoped candidate without changing live state.</summary>
    Task<DomainSharedProjectionRebuildCandidate> CreateEmptyCandidateAsync(
        DomainSharedProjectionRebuildIdentity identity,
        CancellationToken cancellationToken);

    /// <summary>Accumulates one complete, non-erased aggregate history into the candidate.</summary>
    Task<DomainSharedProjectionRebuildCandidate> AccumulateAsync(
        DomainSharedProjectionRebuildIdentity identity,
        DomainSharedProjectionRebuildCandidate candidate,
        ProjectionRequest aggregateHistory,
        CancellationToken cancellationToken);

    /// <summary>Produces the immutable full-replacement/pruning manifest for the sealed candidate.</summary>
    Task<DomainProjectionRebuildPlan> FinalizeAsync(
        DomainSharedProjectionRebuildIdentity identity,
        DomainSharedProjectionRebuildCandidate candidate,
        CancellationToken cancellationToken);
}
