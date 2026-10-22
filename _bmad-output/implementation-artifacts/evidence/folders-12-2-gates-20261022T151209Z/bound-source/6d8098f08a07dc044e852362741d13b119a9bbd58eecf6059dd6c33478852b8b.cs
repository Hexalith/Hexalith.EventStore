using Hexalith.EventStore.Client.Aggregates;
using Hexalith.EventStore.Contracts.Results;

namespace Hexalith.EventStore.Client.Tests.Aggregates;

/// <summary>Exercises the built-in cancellation-aware convention processor and replay seams.</summary>
internal sealed class CancellationFixtureAggregate : EventStoreAggregate<CancellationReplayState>
{
    /// <summary>Processes the fixture command with optional synchronous cancellation.</summary>
    /// <param name="command">The fixture command payload.</param>
    /// <param name="state">The rehydrated aggregate state.</param>
    /// <returns>A no-op result.</returns>
    public static DomainResult Handle(CancellationReplayEvent command, CancellationReplayState? state)
    {
        CancellationTestScope scope = CancellationTestScope.Current;
        scope.Handled++;
        if (scope.CancelInHandle)
        {
            scope.Cancellation.Cancel();
            if (scope.ThrowInHandle)
            {
                scope.Cancellation.Token.ThrowIfCancellationRequested();
            }
        }

        return DomainResult.NoOp();
    }
}
