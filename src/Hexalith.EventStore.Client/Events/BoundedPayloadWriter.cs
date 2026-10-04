using System.Security.Cryptography;

using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.EventStore.Client.Events;

/// <summary>Private, single-use implementation of the approved bounded output writer.</summary>
internal sealed class BoundedPayloadWriter : IBoundedPayloadWriter, IDisposable {
    private readonly CancellationToken _cancellationToken;
    private byte[]? _buffer;
    private int _length;
    private bool _completed;
    private bool _transferred;

    public BoundedPayloadWriter(int maximumBytes, CancellationToken cancellationToken) {
        if (maximumBytes is < 0 or > 1024 * 1024) {
            throw new ArgumentOutOfRangeException(nameof(maximumBytes), "An upcast output is limited to 1 MiB.");
        }

        _cancellationToken = cancellationToken;
        _buffer = GC.AllocateUninitializedArray<byte>(maximumBytes);
    }

    public void Write(ReadOnlySpan<byte> bytes) {
        ObjectDisposedException.ThrowIf(_buffer is null, this);
        if (_completed) {
            throw new InvalidOperationException("The payload writer has already been completed.");
        }

        _cancellationToken.ThrowIfCancellationRequested();
        int nextLength = checked(_length + bytes.Length);
        if (nextLength > _buffer.Length) {
            throw new InvalidOperationException("The payload exceeds its reserved output capacity.");
        }

        bytes.CopyTo(_buffer.AsSpan(_length));
        _length = nextLength;
        _cancellationToken.ThrowIfCancellationRequested();
    }

    public void Complete() {
        ObjectDisposedException.ThrowIf(_buffer is null, this);
        _cancellationToken.ThrowIfCancellationRequested();
        if (_completed) {
            throw new InvalidOperationException("The payload writer can be completed only once.");
        }

        _completed = true;
    }

    /// <summary>Transfers the sealed owner to a private immutable view.</summary>
    internal ImmutablePayload TakeCompletedPayload() {
        ObjectDisposedException.ThrowIf(_buffer is null, this);
        if (!_completed) {
            throw new InvalidOperationException("The payload writer must be completed before its output is consumed.");
        }

        if (_transferred) {
            throw new InvalidOperationException("The completed payload owner has already been transferred.");
        }

        _cancellationToken.ThrowIfCancellationRequested();
        _transferred = true;
        byte[] owner = _buffer;
        _buffer = null;
        return new ImmutablePayload(owner, _length, _cancellationToken);
    }

    public void Dispose() {
        byte[]? buffer = Interlocked.Exchange(ref _buffer, null);
        if (buffer is not null) {
            CryptographicOperations.ZeroMemory(buffer);
        }
    }
}
