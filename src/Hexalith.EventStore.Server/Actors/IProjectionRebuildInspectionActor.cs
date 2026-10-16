using Dapr.Actors;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>Additive read-only recovery capability; existing rebuild writer methods retain their ABI.</summary>
public interface IProjectionRebuildInspectionActor : IActor
{
    /// <summary>Inspects original staged/promotion evidence without publishing, repairing or notifying.</summary>
    Task<ProjectionRebuildInspection> InspectProjectionRebuildAsync(ProjectionRebuildCandidateOperation request);
}
