using Hexalith.EventStore.Client.Handlers;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Sample.Counter.Events;
using Hexalith.EventStore.Sample.Counter.State;

using Shouldly;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Events;

/// <summary>Checks the actual platform fold of a restored snapshot and tail before handling.</summary>
internal sealed class P1RCountProbeProcessor(int expectedCount, bool append) : DomainProcessorBase<CounterState>
{
    /// <summary>Gets the state count observed by the actual SDK fold.</summary>
    public int? ObservedCount { get; private set; }

    /// <inheritdoc/>
    protected override Task<DomainResult> HandleAsync(CommandEnvelope command, CounterState? currentState)
    {
        currentState.ShouldNotBeNull().Count.ShouldBe(expectedCount);
        ObservedCount = currentState.Count;
        return Task.FromResult(append ? DomainResult.Success([new CounterIncremented()]) : DomainResult.NoOp());
    }
}
