using Hexalith.EventStore.Client.Projections;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Integration;

/// <summary>Stops a fixture-owned batch at an exact phase without automatic reconciliation masking retained progress.</summary>
internal sealed class RetainedBatchLiveFaultInjector(ReadModelBatchPhase target, int targetOrdinal) : IReadModelBatchFaultInjector
{
    public Task InjectAsync(ReadModelBatchPhase phase, int ordinal, CancellationToken cancellationToken)
        => phase == target && ordinal == targetOrdinal
            ? throw new InvalidOperationException("Fixture-owned batch process stop.") : Task.CompletedTask;
}
