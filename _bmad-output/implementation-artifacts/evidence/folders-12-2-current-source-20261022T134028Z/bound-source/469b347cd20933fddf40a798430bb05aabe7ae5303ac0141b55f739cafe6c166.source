using Hexalith.EventStore.Client.Projections;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Integration;

/// <summary>Terminates a real worker at an exact durable protocol transition.</summary>
internal sealed class ProjectionBoundedIdentityProcessExitInjector(ReadModelBatchPhase targetPhase, int targetOrdinal,
    Func<Task> beforeExit) : IReadModelBatchFaultInjector
{
    public async Task InjectAsync(ReadModelBatchPhase phase, int ordinal, CancellationToken cancellationToken)
    {
        if (phase == targetPhase && ordinal == targetOrdinal)
        {
            await beforeExit();
            Environment.Exit(73);
        }
    }
}
