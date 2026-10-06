using Hexalith.EventStore.Contracts.Aggregates;

namespace Hexalith.EventStore.Client.Tests.Aggregates;

/// <summary>Tracks event effects and cancels from synchronous domain callbacks.</summary>
internal sealed class CancellationReplayState : ITerminatable
{
    private int _count;

    /// <summary>Gets the state count, optionally cancelling successor serialization.</summary>
    public int Count
    {
        get
        {
            if (_count > 0 && CancellationTestScope.Current.CancelInStateGetter)
            {
                CancellationTestScope.Current.Cancellation.Cancel();
            }

            return _count;
        }
    }

    /// <inheritdoc/>
    public bool IsTerminated
    {
        get
        {
            if (CancellationTestScope.Current.CancelInTerminationGetter)
            {
                CancellationTestScope.Current.Cancellation.Cancel();
                return true;
            }

            return false;
        }
    }

    /// <summary>Applies one event and optionally cancels or throws cancellation.</summary>
    /// <param name="payload">The payload being applied.</param>
    public void Apply(CancellationReplayEvent payload)
    {
        CancellationTestScope scope = CancellationTestScope.Current;
        scope.Applied++;
        _count++;
        if (scope.CancelAfterApply == scope.Applied)
        {
            scope.Cancellation.Cancel();
            if (scope.ThrowInApply)
            {
                scope.Cancellation.Token.ThrowIfCancellationRequested();
            }
        }
    }
}
