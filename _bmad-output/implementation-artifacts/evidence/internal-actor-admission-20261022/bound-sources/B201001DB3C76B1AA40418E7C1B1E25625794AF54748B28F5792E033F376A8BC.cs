using Hexalith.EventStore.Client.Aggregates;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Sample.Counter.Events;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.DomainServices;

/// <summary>
/// Test aggregate that decrements only when prior events were rehydrated correctly.
/// </summary>
internal sealed class RoundTripCounterAggregate : EventStoreAggregate<RoundTripCounterState>
{
    /// <summary>
    /// Handles the decrement proof command.
    /// </summary>
    /// <param name="command">The decrement command.</param>
    /// <param name="state">The current rehydrated counter state.</param>
    /// <returns>A success result only when the state proves previous events were replayed.</returns>
    public static DomainResult Handle(TestDecrementCounter command, RoundTripCounterState? state)
    {
        _ = command;
        if ((state?.Count ?? 0) == 0)
        {
            return DomainResult.Rejection(System.Array.Empty<IRejectionEvent>());
        }

        return DomainResult.Success(new IEventPayload[]
        {
            new CounterDecremented(),
        });
    }
}
