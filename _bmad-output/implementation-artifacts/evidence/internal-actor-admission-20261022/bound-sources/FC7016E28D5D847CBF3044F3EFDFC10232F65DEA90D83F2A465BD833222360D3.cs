using Dapr.Actors;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>
/// DAPR actor interface for allocating cross-aggregate event positions.
/// A single actor instance serializes allocations and stores the latest assigned position.
/// </summary>
public interface IGlobalPositionActor : IActor {
    /// <summary>
    /// Allocates a contiguous range of global positions.
    /// </summary>
    /// <param name="count">The number of positions to allocate.</param>
    /// <returns>The first allocated global position.</returns>
    Task<long> AllocateAsync(int count);

    /// <summary>
    /// Gets the latest allocated global position.
    /// </summary>
    /// <returns>The current global position, or zero when none has been allocated.</returns>
    Task<long> GetCurrentAsync();
}
