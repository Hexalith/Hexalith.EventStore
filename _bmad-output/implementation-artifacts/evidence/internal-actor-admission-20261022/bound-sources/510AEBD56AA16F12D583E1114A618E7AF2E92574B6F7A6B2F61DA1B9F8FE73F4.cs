using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.EventStore.Client.Events;

/// <summary>Exposes copying only while one synchronous or asynchronous upcaster invocation owns its read lease.</summary>
internal sealed class InvocationPayloadLease(IReadOnlyPayload payload, CancellationToken cancellationToken) : IReadOnlyPayload, IDisposable
{
    private readonly object _leaseLock = new();
    private bool _disposed;

    /// <inheritdoc/>
    public int Length
    {
        get
        {
            lock (_leaseLock)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                cancellationToken.ThrowIfCancellationRequested();
                return payload.Length;
            }
        }
    }

    /// <inheritdoc/>
    public void CopyTo(int sourceOffset, Span<byte> destination)
    {
        lock (_leaseLock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            cancellationToken.ThrowIfCancellationRequested();
            payload.CopyTo(sourceOffset, destination);
            cancellationToken.ThrowIfCancellationRequested();
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        lock (_leaseLock)
        {
            _disposed = true;
        }
    }
}
