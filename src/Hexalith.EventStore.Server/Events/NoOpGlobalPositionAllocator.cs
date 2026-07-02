namespace Hexalith.EventStore.Server.Events;

internal sealed class NoOpGlobalPositionAllocator : IGlobalPositionAllocator {
    public static NoOpGlobalPositionAllocator Instance { get; } = new();

    public Task<long> AllocateAsync(int count, CancellationToken cancellationToken = default) {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        return Task.FromResult(0L);
    }
}
