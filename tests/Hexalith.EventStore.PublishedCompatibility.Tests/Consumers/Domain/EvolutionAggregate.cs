#if SELECTED
using Hexalith.EventStore.Client.Aggregates;
using Hexalith.EventStore.Contracts.Results;

namespace PublishedConsumer;

/// <summary>Receives its evolution registry through AddEventStoreClient rather than direct construction.</summary>
public sealed class EvolutionAggregate : EventStoreAggregate<EvolutionState>
{
    public int ObservedCount { get; private set; }

    public DomainResult Handle(InspectEvolution command, EvolutionState? state)
    {
        ObservedCount = state?.Count ?? 0;
        return DomainResult.NoOp();
    }
}
#endif
