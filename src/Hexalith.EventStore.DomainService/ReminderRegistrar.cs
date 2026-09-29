using Hexalith.EventStore.Client.Reminders;
using Hexalith.EventStore.Contracts.Reminders;

namespace Hexalith.EventStore.DomainService;

/// <summary>
/// Default <see cref="IReminderRegistrar"/>: validates the target, routes convergence into the item's reminder
/// actor turn, and records the item's retained totals for readiness.
/// </summary>
internal sealed class ReminderRegistrar(IReminderActorInvoker invoker, ReminderRuntimeStatus status) : IReminderRegistrar
{
    private readonly IReminderActorInvoker _invoker = invoker ?? throw new ArgumentNullException(nameof(invoker));
    private readonly ReminderRuntimeStatus _status = status ?? throw new ArgumentNullException(nameof(status));

    /// <inheritdoc/>
    public async Task<ReminderConvergenceResult> ConvergeAsync(
        ReminderTarget target,
        CancellationToken cancellationToken = default)
    {
        string actorId = ReminderCoordinator.ComputeActorId(target);
        ReminderConvergenceResult result = await _invoker
            .ConvergeAsync(actorId, target, cancellationToken)
            .ConfigureAwait(false);
        _status.RecordItem(actorId, result.Unresolved, result.Quarantined);
        return result;
    }
}
