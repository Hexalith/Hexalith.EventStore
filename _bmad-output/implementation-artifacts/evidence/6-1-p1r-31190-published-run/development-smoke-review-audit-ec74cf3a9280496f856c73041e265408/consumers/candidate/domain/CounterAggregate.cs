using Hexalith.EventStore.Client.Aggregates;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Results;

namespace P1R.Counter;

/// <summary>Disposable Counter semantics executed by the published domain SDK.</summary>
public sealed class CounterAggregate : EventStoreAggregate<CounterState>
{
    /// <summary>Appends one increment through the real actor persistence path.</summary>
    public static DomainResult Handle(IncrementCounter command, CounterState? state)
        => DomainResult.Success(new IEventPayload[] { new CounterIncremented() });

    /// <summary>Asserts the state supplied by actor hydration without appending events.</summary>
    public static DomainResult Handle(AssertCounter command, CounterState? state)
    {
        if ((state?.Count ?? 0) != command.Expected)
        {
            throw new InvalidOperationException("Counter hydration assertion failed.");
        }

        return DomainResult.NoOp();
    }
}
