using System.Reflection;

using Dapr.Actors.Runtime;

namespace Hexalith.EventStore.Server.Tests.TestUtilities;

/// <summary>
/// Shared Dapr actor test helpers.
/// </summary>
internal static class ActorStateManagerTestHelper {
    private static readonly PropertyInfo StateManagerProperty =
        typeof(Actor).GetProperty("StateManager", BindingFlags.Public | BindingFlags.Instance)
        ?? throw new InvalidOperationException("Dapr Actor.StateManager property was not found.");

    /// <summary>
    /// Assigns the runtime-managed state manager for actors created with <see cref="ActorHost.CreateForTest{TActor}"/>.
    /// </summary>
    /// <param name="actor">The actor instance under test.</param>
    /// <param name="stateManager">The test state manager to assign.</param>
    internal static void SetStateManager(Actor actor, IActorStateManager stateManager) {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(stateManager);

        StateManagerProperty.SetValue(actor, stateManager);
    }
}
