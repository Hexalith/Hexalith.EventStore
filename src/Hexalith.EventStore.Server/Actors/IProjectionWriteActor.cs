
using Dapr.Actors;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>
/// DAPR actor interface for writing projection state.
/// The projection builder (Story 11-3) calls this after receiving
/// state from the domain service's /project endpoint.
/// </summary>
public interface IProjectionWriteActor : IActor {
    /// <summary>
    /// Persists projection state, regenerates ETag, and broadcasts SignalR notification.
    /// </summary>
    /// <param name="state">The projection state received from the domain service.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task UpdateProjectionAsync(ProjectionState state);
}
