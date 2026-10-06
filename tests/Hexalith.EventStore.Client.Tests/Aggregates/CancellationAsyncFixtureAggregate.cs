using Hexalith.EventStore.Client.Aggregates;
using Hexalith.EventStore.Contracts.Results;

namespace Hexalith.EventStore.Client.Tests.Aggregates;

/// <summary>Returns an in-flight legacy task after cancelling during its synchronous prelude.</summary>
internal sealed class CancellationAsyncFixtureAggregate : EventStoreAggregate<CancellationReplayState>
{
    /// <summary>Gets the completion source for the already-started handler.</summary>
    internal TaskCompletionSource<DomainResult> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Starts legacy async work and cancels before returning its task.</summary>
    /// <param name="command">The fixture command.</param>
    /// <param name="state">The rehydrated prior state.</param>
    /// <returns>The task that the adapter must observe through completion.</returns>
    public Task<DomainResult> Handle(CancellationReplayEvent command, CancellationReplayState? state)
    {
        CancellationTestScope.Current.Cancellation.Cancel();
        return Completion.Task;
    }
}
