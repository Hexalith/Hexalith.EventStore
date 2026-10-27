#if SELECTED
using Hexalith.EventStore.Client.Aggregates;
using Hexalith.EventStore.Contracts.Results;

namespace PublishedConsumer;

/// <summary>Receives its evolution registry through AddEventStoreClient rather than direct construction.</summary>
public sealed class EvolutionAggregate : EventStoreAggregate<EvolutionState>
{
    /// <summary>Gets the value reconstructed by the registered evolution-aware aggregate.</summary>
    public int ObservedCount { get; private set; }

    /// <summary>Observes the reconstructed evolved state without producing new events.</summary>
    public DomainResult Handle(InspectEvolution command, EvolutionState? state)
    {
        ObservedCount = state?.Count ?? 0;
        return DomainResult.NoOp();
    }
}
#endif
