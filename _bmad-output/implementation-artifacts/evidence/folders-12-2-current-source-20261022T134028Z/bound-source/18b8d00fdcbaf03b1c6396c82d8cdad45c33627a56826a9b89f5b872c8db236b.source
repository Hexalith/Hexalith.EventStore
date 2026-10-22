using Hexalith.EventStore.Client.Projections;

namespace Hexalith.EventStore.Client.Tests.Projections;

internal sealed class PendingBatchRecoveryStopAtPhase(ReadModelBatchPhase target) : IReadModelBatchFaultInjector
{
    public Task InjectAsync(ReadModelBatchPhase phase, int ordinal, CancellationToken cancellationToken)
    {
        if (phase == ReadModelBatchPhase.Reconcile || phase == target) throw new IOException("injected interruption");
        return Task.CompletedTask;
    }
}
