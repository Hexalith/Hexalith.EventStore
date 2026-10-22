using Hexalith.EventStore.Contracts.Reminders;

namespace Hexalith.EventStore.DomainService.Tests.Fixtures;

/// <summary>Runs convergence inside the harness's serialized per-actor turn, standing in for Dapr placement.</summary>
internal sealed class InProcessReminderActorInvoker(ReminderTestHarness harness, ReminderRuntimeStatus status) : IReminderActorInvoker
{
    /// <inheritdoc/>
    public Task<ReminderConvergenceResult> ConvergeAsync(string actorId, ReminderTarget target, CancellationToken cancellationToken)
        => harness.InTurnAsync(
            actorId,
            () => harness.CreateCoordinator(status).ConvergeAsync(actorId, target, harness.SchedulerFor(actorId), cancellationToken));
}
