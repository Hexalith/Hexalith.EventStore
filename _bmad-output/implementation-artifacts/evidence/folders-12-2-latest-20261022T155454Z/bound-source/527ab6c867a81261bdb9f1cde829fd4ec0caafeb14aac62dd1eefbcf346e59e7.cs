using Hexalith.EventStore.Client.Aggregates;
using Hexalith.EventStore.Contracts.Replay;
using Hexalith.EventStore.DomainService.Tests.Fixtures;

namespace Hexalith.EventStore.DomainService.Tests;

/// <summary>Reimplements async replay while inheriting the built-in admitted-input seam.</summary>
internal sealed class CustomAsyncReplayAggregate : EventStoreAggregate<WidgetState>, IAsyncAggregateReplay
{
    Task<AggregateReconstructionResult> IAsyncAggregateReplay.ReplayAsync(
        AggregateReconstructionRequest request, CancellationToken cancellationToken)
        => Task.FromResult(AggregateReconstructionResult.Succeeded("{\"customAsync\":true}", 0));
}
