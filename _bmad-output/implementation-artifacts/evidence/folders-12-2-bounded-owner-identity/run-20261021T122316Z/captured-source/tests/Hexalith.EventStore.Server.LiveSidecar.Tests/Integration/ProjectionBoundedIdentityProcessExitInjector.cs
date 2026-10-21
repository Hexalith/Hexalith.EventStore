using Hexalith.EventStore.Client.Projections;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Integration;

/// <summary>Terminates a real worker after durable preparation, before any participant exists.</summary>
internal sealed class ProjectionBoundedIdentityProcessExitInjector(Action beforeExit) : IReadModelBatchFaultInjector
{
    public Task InjectAsync(ReadModelBatchPhase phase, int ordinal, CancellationToken cancellationToken)
    {
        if (phase == ReadModelBatchPhase.MarkerPrepared)
        {
            beforeExit();
            Environment.Exit(73);
        }
        return Task.CompletedTask;
    }
}
