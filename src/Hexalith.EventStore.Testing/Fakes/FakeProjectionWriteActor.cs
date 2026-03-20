
using Hexalith.EventStore.Server.Actors;

namespace Hexalith.EventStore.Testing.Fakes;
/// <summary>
/// Fake projection write actor for testing. Records the last received state for assertion.
/// </summary>
public class FakeProjectionWriteActor : IProjectionWriteActor {
    /// <summary>Gets the last state received via <see cref="UpdateProjectionAsync"/>.</summary>
    public ProjectionState? LastReceivedState { get; private set; }

    /// <inheritdoc/>
    public Task UpdateProjectionAsync(ProjectionState state) {
        LastReceivedState = state;
        return Task.CompletedTask;
    }
}
