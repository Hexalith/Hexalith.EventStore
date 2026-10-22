namespace Hexalith.EventStore.Server.Events;

/// <summary>
/// Allocates cross-aggregate event positions for persisted event envelopes.
/// </summary>
public interface IGlobalPositionAllocator {
    /// <summary>
    /// Allocates a contiguous range of global event positions.
    /// </summary>
    /// <param name="count">The number of positions to allocate.</param>
    /// <param name="cancellationToken">A token used by local callers before actor invocation.</param>
    /// <returns>The first allocated global position.</returns>
    Task<long> AllocateAsync(int count, CancellationToken cancellationToken = default);
}
