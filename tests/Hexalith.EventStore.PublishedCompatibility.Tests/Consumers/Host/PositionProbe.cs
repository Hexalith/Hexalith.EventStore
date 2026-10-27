using Hexalith.EventStore.Server.Events;

namespace PublishedConsumer;

/// <summary>Counts attempted global allocations without granting provider mutation authority.</summary>
public sealed class PositionProbe : IGlobalPositionAllocator
{
    public int Calls { get; private set; }

    public Task<long> AllocateAsync(int count, CancellationToken cancellationToken = default)
    {
        Calls++;
        return Task.FromResult(987L);
    }
}
