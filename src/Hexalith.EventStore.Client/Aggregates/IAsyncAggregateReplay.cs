using Hexalith.EventStore.Contracts.Replay;

namespace Hexalith.EventStore.Client.Aggregates;

/// <summary>Replays an explicitly owned aggregate route with request cancellation.</summary>
public interface IAsyncAggregateReplay
{
    /// <summary>Returns whether this handler owns the requested aggregate type.</summary>
    /// <param name="aggregateType">The addressed aggregate type.</param>
    /// <returns>True when this handler owns the aggregate type.</returns>
    bool CanReplayAggregateType(string aggregateType);

    /// <summary>Replays a request without publishing intermediate state.</summary>
    /// <param name="request">The aggregate reconstruction request.</param>
    /// <param name="cancellationToken">The originating request cancellation token.</param>
    /// <returns>The reconstruction result.</returns>
    Task<AggregateReconstructionResult> ReplayAsync(AggregateReconstructionRequest request, CancellationToken cancellationToken);
}
