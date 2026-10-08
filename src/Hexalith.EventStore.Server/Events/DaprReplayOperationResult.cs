namespace Hexalith.EventStore.Server.Events;

/// <summary>Returns durable-boundary truth without conflating cancellation, acknowledgement or uncertain commit.</summary>
/// <param name="Outcome">The exact logical readback classification.</param>
/// <param name="Generation">The observed current owner generation.</param>
/// <param name="Response">The detached exact pinned response, present only after proven readback.</param>
/// <param name="IsComplete">Whether the final result participant was also proven.</param>
/// <param name="OriginatingCancellationObserved">Whether original cancellation withheld a committed response.</param>
/// <param name="ResponseUnavailable">Whether current source/trust loss withheld a committed response.</param>
internal sealed record DaprReplayOperationResult(DaprReplayCommitOutcome Outcome, long Generation, DaprLogicalResponseOwner? Response,
    bool IsComplete, bool OriginatingCancellationObserved = false, bool ResponseUnavailable = false) : IDisposable
{
    /// <summary>Gets canonical state only after its exact page/final readback is proven.</summary>
    internal DaprLogicalResponseOwner? CanonicalState { get; init; }
    /// <inheritdoc/>
    public void Dispose() { Response?.Dispose(); CanonicalState?.Dispose(); }
}
