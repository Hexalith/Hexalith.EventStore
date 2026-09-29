using Dapr.Actors;

using Hexalith.EventStore.Contracts.Reminders;

namespace Hexalith.EventStore.DomainService;

/// <summary>
/// Dapr actor contract of the typed-reminder runtime. One actor, identified by the <c>wra-</c> digest of its
/// tenant item, owns every reminder of that item, so registration and callbacks for one item serialize.
/// </summary>
public interface IReminderActor : IActor
{
    /// <summary>Converges the actor's reminders with the target's current intents.</summary>
    /// <param name="target">The target stream; it must re-derive this actor's identifier.</param>
    /// <returns>The convergence counts.</returns>
    Task<ReminderConvergenceResult> ConvergeAsync(ReminderTarget target);
}
