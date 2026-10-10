using Hexalith.EventStore.Client.Aggregates;
using Hexalith.EventStore.Contracts.Results;

namespace Hexalith.EventStore.Client.Tests.Aggregates;

/// <summary>Records the state reached before command handling.</summary>
internal sealed class VersionTwoTestAggregate : EventStoreAggregate<VersionTwoTestState>
{
    /// <summary>Gets the value observed by the command handler.</summary>
    internal int ObservedValue { get; private set; }

    /// <summary>Handles the command after historical event application.</summary>
    public DomainResult Handle(VersionTwoTestCommand command, VersionTwoTestState? state)
    {
        ArgumentNullException.ThrowIfNull(command);
        ObservedValue = state?.Value ?? 0;
        return DomainResult.NoOp();
    }
}
