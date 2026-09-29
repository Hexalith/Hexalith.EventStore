using Hexalith.EventStore.Contracts.Reminders;

namespace Hexalith.EventStore.DomainService;

/// <summary>Routes a convergence request into the serialized turn of the item's reminder actor.</summary>
internal interface IReminderActorInvoker
{
    /// <summary>Converges one item inside its reminder actor turn.</summary>
    /// <param name="actorId">The <c>wra-</c> actor identifier derived from the target.</param>
    /// <param name="target">The target stream.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The convergence counts.</returns>
    Task<ReminderConvergenceResult> ConvergeAsync(string actorId, ReminderTarget target, CancellationToken cancellationToken);
}
