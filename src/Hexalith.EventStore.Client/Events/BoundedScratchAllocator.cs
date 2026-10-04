using System.Security.Cryptography;

using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.EventStore.Client.Events;

/// <summary>Private callback-scoped scratch allocator with deterministic clearing.</summary>
internal sealed class BoundedScratchAllocator : IBoundedScratchAllocator {
    private readonly int _maximumBytes;

    public BoundedScratchAllocator(int maximumBytes) {
        if (maximumBytes is < 0 or > 128 * 1024 * 1024) {
            throw new ArgumentOutOfRangeException(nameof(maximumBytes));
        }

        _maximumBytes = maximumBytes;
    }

    public void WithScratch(int requestedCapacity, ScratchSpanAction action, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(action);
        cancellationToken.ThrowIfCancellationRequested();
        if (requestedCapacity < 0 || requestedCapacity > _maximumBytes) {
            throw new ArgumentOutOfRangeException(nameof(requestedCapacity));
        }

        byte[] owner = GC.AllocateUninitializedArray<byte>(requestedCapacity);
        try {
            cancellationToken.ThrowIfCancellationRequested();
            action(owner.AsSpan());
            cancellationToken.ThrowIfCancellationRequested();
        }
        finally {
            CryptographicOperations.ZeroMemory(owner);
        }
    }
}
