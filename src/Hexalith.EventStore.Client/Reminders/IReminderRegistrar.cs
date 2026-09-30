using Hexalith.EventStore.Contracts.Reminders;

namespace Hexalith.EventStore.Client.Reminders;

/// <summary>Converges a target's durable reminders with the intents its stream currently holds.</summary>
/// <remarks>
/// <para>
/// Call <see cref="ConvergeAsync"/> at least once after every committed event that changes a target's
/// schedule, and retry it when it throws, for example by letting a domain-event handler fail so its delivery
/// is redelivered. A convergence that throws may have persisted nothing, so the periodic reconciler cannot
/// rediscover the item.
/// </para>
/// <para>
/// A host that maps the Dapr actor handlers itself must call <c>MapActorsHandlers</c> before
/// <c>UseEventStoreDomainService</c>; mapping them a second time makes the actor routes ambiguous.
/// </para>
/// </remarks>
public interface IReminderRegistrar
{
    /// <summary>
    /// Re-folds the target's intents, persists their witnesses before scheduling, arms future reminders,
    /// submits due ones, and cancels obsolete ones. Repeating the call is safe.
    /// </summary>
    /// <param name="target">The target stream, usually the one a committed event just changed.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The convergence counts; <see cref="ReminderConvergenceResult.Unresolved"/> is retained work.</returns>
    Task<ReminderConvergenceResult> ConvergeAsync(
        ReminderTarget target,
        CancellationToken cancellationToken = default);
}
