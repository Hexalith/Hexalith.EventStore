using Hexalith.EventStore.Client.Aggregates;
using Hexalith.EventStore.Contracts.Results;

namespace Hexalith.EventStore.Server.Tests.DomainServices;

/// <summary>Produces a typed version-two event from upcast history.</summary>
internal sealed class VersionedActorAggregate : EventStoreAggregate<VersionedActorState>
{
    /// <summary>Gets the state observed before the command.</summary>
    internal int ObservedValue { get; private set; }

    /// <summary>Handles the test command after state rehydration.</summary>
    public DomainResult Handle(VersionedActorCommand command, VersionedActorState? state)
    {
        ArgumentNullException.ThrowIfNull(command);
        ObservedValue = state?.Value ?? 0;
        return DomainResult.Success([new VersionedActorEvent(ObservedValue + 1)]);
    }
}
