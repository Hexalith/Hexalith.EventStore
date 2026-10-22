using Hexalith.EventStore.Client.Projections;

namespace Hexalith.EventStore.Client.Tests.Projections;

internal sealed class NativeOwnerRecoveryFault(ReadModelBatchPhase phase, int ordinal) : IReadModelBatchFaultInjector
{
    public bool Fired { get; private set; }
    public Task InjectAsync(ReadModelBatchPhase current, int operation, CancellationToken cancellationToken = default)
    {
        if (!Fired && current == phase && operation == ordinal)
        {
            Fired = true;
            throw new InvalidOperationException("interrupt original selected generation");
        }
        return Task.CompletedTask;
    }
}
