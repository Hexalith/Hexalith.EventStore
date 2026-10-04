using System.Security.Cryptography;

using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.EventStore.Client.Events;

/// <summary>Private callback-scoped scratch allocator with deterministic clearing.</summary>
internal sealed class BoundedScratchAllocator : IBoundedScratchAllocator, IDisposable {
    private readonly int _maximumBytes;
    private readonly EventBufferBudget _budget;
    private readonly CancellationToken _invocationCancellationToken;
    private readonly object _invocationLock = new();
    private bool _disposed;
    private bool _contractViolated;
    private int _activeCallbacks;

    public BoundedScratchAllocator(int maximumBytes, EventBufferBudget? budget = null, CancellationToken invocationCancellationToken = default) {
        if (maximumBytes is < 0 or > 128 * 1024 * 1024) {
            throw new ArgumentOutOfRangeException(nameof(maximumBytes));
        }

        _maximumBytes = maximumBytes;
        _budget = budget ?? new EventBufferBudget(maximumBytes);
        _invocationCancellationToken = invocationCancellationToken;
    }

    public void WithScratch(int requestedCapacity, ScratchSpanAction action, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(action);
        cancellationToken.ThrowIfCancellationRequested();
        _invocationCancellationToken.ThrowIfCancellationRequested();
        if (requestedCapacity < 0 || requestedCapacity > _maximumBytes) {
            throw new ArgumentOutOfRangeException(nameof(requestedCapacity));
        }

        lock (_invocationLock) {
            if (_disposed) {
                _contractViolated = true;
                throw new ObjectDisposedException(nameof(BoundedScratchAllocator), "UpcasterContractViolation: the invocation scratch lease has ended.");
            }

            _activeCallbacks++;
        }

        EventBufferReservation? reservation = null;
        byte[]? owner = null;
        try {
            reservation = _budget.Reserve(requestedCapacity);
            owner = new byte[requestedCapacity];
            cancellationToken.ThrowIfCancellationRequested();
            _invocationCancellationToken.ThrowIfCancellationRequested();
            action(owner.AsSpan());
            cancellationToken.ThrowIfCancellationRequested();
            _invocationCancellationToken.ThrowIfCancellationRequested();
        }
        finally {
            if (owner is not null) {
                CryptographicOperations.ZeroMemory(owner);
            }

            reservation?.Dispose();
            lock (_invocationLock) {
                _activeCallbacks--;
            }
        }
    }

    /// <summary>Refuses acceptance after an allocator escaped its invocation or retained a running callback.</summary>
    internal void RequireValidInvocation() {
        lock (_invocationLock) {
            if (_contractViolated) {
                throw new InvalidOperationException("UpcasterContractViolation: scratch allocation escaped its invocation.");
            }
        }
    }

    /// <inheritdoc/>
    public void Dispose() {
        lock (_invocationLock) {
            _disposed = true;
            if (_activeCallbacks != 0) {
                _contractViolated = true;
            }
        }
    }
}
