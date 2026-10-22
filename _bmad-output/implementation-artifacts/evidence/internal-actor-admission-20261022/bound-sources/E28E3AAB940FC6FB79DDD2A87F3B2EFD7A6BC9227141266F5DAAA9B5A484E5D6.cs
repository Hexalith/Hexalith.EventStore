namespace Hexalith.EventStore.Contracts.Events;

/// <summary>Provides scratch spans whose capacity and lifetime are bounded by the pipeline.</summary>
public interface IBoundedScratchAllocator {
    /// <summary>Runs one synchronous operation with an exclusively leased scratch span.</summary>
    /// <param name="requestedCapacity">The exact scratch capacity requested.</param>
    /// <param name="action">The callback that uses the temporary span.</param>
    /// <param name="cancellationToken">The operation cancellation token.</param>
    void WithScratch(int requestedCapacity, ScratchSpanAction action, CancellationToken cancellationToken);
}
