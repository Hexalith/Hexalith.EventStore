using Dapr.Actors;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>Additive actor-side lifecycle-fenced publication for compatible full replay.</summary>
public interface IProjectionReplayPublicationActor : IActor
{
    /// <summary>Stages the immutable complete candidate, recovering an ambiguous save read-only.</summary>
    Task<ProjectionRebuildInspection> StageReplayAsync(ProjectionReplayPublicationRequest request);

    /// <summary>Publishes the original candidate once under the current supplied lifecycle fence.</summary>
    Task<ProjectionRebuildInspection> PromoteReplayAsync(ProjectionReplayPublicationRequest request);

    /// <summary>Compacts completed rollback bytes while retaining a bounded same-work tombstone.</summary>
    Task<bool> FinalizeReplayAsync(ProjectionReplayPublicationRequest request);

    /// <summary>Verifies an already selected exact prefix/model without creating a new receipt or publication.</summary>
    Task<bool> ReadReplayCompletionAsync(ProjectionReplayPublicationRequest request);
}
