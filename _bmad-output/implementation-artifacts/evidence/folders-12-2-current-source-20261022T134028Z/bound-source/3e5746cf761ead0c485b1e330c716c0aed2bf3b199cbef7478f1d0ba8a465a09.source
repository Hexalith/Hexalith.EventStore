using Hexalith.EventStore.Client.Aggregates;
using Hexalith.EventStore.Contracts.Replay;
using Hexalith.EventStore.DomainService.Tests.Fixtures;

namespace Hexalith.EventStore.DomainService.Tests;

/// <summary>Reimplements synchronous replay while inheriting the built-in admitted-input seam.</summary>
internal sealed class CustomSyncReplayAggregate : EventStoreAggregate<WidgetState>, IAggregateReplay
{
    AggregateReconstructionResult IAggregateReplay.Replay(AggregateReconstructionRequest request)
        => AggregateReconstructionResult.Succeeded("{\"customSync\":true}", 0);
}
