using Dapr.Actors;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>Additive strict completion capability for unactivated-writer replay recovery.</summary>
public interface IProjectionBoundReplayCompletionActor : IActor
{
    /// <summary>Verifies a fully bound completed receipt, or finalizes only its exact original unfinished publication.</summary>
    Task<bool> ReconcileBoundReplayCompletionAsync(ProjectionReplayPublicationRequest request);
}
