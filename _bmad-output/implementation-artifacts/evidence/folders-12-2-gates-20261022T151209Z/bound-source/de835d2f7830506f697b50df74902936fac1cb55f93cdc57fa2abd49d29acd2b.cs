using Hexalith.EventStore.Client.Projections;

namespace Hexalith.EventStore.Client.Tests.Projections;

internal sealed class DelegatingBatchFaultInjector(Func<ReadModelBatchPhase, int, CancellationToken, Task> fault) : IReadModelBatchFaultInjector
{
    public Task InjectAsync(ReadModelBatchPhase phase, int ordinal, CancellationToken cancellationToken)
        => fault(phase, ordinal, cancellationToken);
}
