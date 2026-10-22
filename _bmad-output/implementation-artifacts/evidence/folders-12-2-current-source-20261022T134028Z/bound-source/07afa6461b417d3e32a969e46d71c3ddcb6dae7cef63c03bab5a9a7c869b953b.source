using Hexalith.EventStore.Server.Events;

namespace Hexalith.EventStore.Testing.Integration.Tests.Benchmarking;

internal sealed class RecordingGlobalPositionAllocator(long firstGlobalPosition) : IGlobalPositionAllocator {
    private int _allocationCount;
    private int _requestedCount;

    internal int AllocationCount => Volatile.Read(ref _allocationCount);

    internal Exception? AllocationException { get; set; }

    internal int RequestedCount => Volatile.Read(ref _requestedCount);

    public Task<long> AllocateAsync(int count, CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        Interlocked.Exchange(ref _requestedCount, count);
        Interlocked.Increment(ref _allocationCount);
        if (AllocationException is not null) {
            return Task.FromException<long>(AllocationException);
        }

        return Task.FromResult(firstGlobalPosition);
    }
}
