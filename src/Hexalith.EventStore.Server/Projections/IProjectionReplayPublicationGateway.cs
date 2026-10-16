using Hexalith.EventStore.Server.Actors;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>Owning actor seam for lifecycle-fenced compatible complete replay publication.</summary>
internal interface IProjectionReplayPublicationGateway
{
    /// <summary>Stages or recovers the original immutable complete candidate.</summary>
    Task<ProjectionRebuildInspection> StageReplayAsync(string actorId, ProjectionReplayPublicationRequest request,
        CancellationToken cancellationToken);

    /// <summary>Promotes once, or reads the original matching durable promotion.</summary>
    Task<ProjectionRebuildInspection> PromoteReplayAsync(string actorId, ProjectionReplayPublicationRequest request,
        CancellationToken cancellationToken);

    /// <summary>Compacts matching completed model bytes to the bounded original-work binding.</summary>
    Task<bool> FinalizeReplayAsync(string actorId, ProjectionReplayPublicationRequest request,
        CancellationToken cancellationToken);

    /// <summary>Verifies an exact already completed prefix/model through its owning actor without repair.</summary>
    Task<bool> ReadReplayCompletionAsync(string actorId, ProjectionReplayPublicationRequest request,
        CancellationToken cancellationToken);
}
