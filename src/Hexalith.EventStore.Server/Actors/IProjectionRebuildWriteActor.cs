using Dapr.Actors;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>Optional projection-actor capability for operation-scoped rebuild staging.</summary>
public interface IProjectionRebuildWriteActor : IActor {
    /// <summary>Persists a non-live candidate for the matching rebuild operation.</summary>
    Task StageProjectionAsync(ProjectionRebuildCandidate request);

    /// <summary>Atomically replaces live state with the matching staged candidate.</summary>
    Task<bool> PromoteProjectionAsync(ProjectionRebuildCandidateOperation request);

    /// <summary>Discards the matching non-live candidate without changing live state.</summary>
    Task<bool> DiscardProjectionAsync(ProjectionRebuildCandidateOperation request);
}
