using Hexalith.EventStore.Contracts.Replay;

namespace Hexalith.EventStore.Client.Aggregates;

/// <summary>Lets the built-in legacy replay engine borrow the router's admitted private input.</summary>
/// <remarks>This internal seam grants no paged, authenticated or versioned replay authority.</remarks>
internal interface IAdmittedLegacyAggregateReplay
{
    /// <summary>Checks whether the selected public interface still dispatches to the built-in replay engine.</summary>
    /// <param name="asynchronous">Whether the router selected the asynchronous replay interface.</param>
    /// <returns>True only when the selected interface method belongs to the built-in base implementation.</returns>
    bool CanReplayAdmitted(bool asynchronous);

    /// <summary>Replays without making a second complete private payload copy.</summary>
    /// <param name="request">The admitted legacy request.</param>
    /// <param name="input">The router-owned input retained through the call.</param>
    /// <param name="cancellationToken">The originating request cancellation token.</param>
    /// <returns>The complete reconstruction result.</returns>
    AggregateReconstructionResult ReplayAdmitted(AggregateReconstructionRequest request,
        LegacyReplayInput input, CancellationToken cancellationToken);
}
