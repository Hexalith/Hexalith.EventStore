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
    private EventBufferReservation? _reservation;
    private readonly object _writerLock = new();
    private bool _contractViolated;

    public BoundedPayloadWriter(int maximumBytes, CancellationToken cancellationToken, EventBufferBudget? budget = null)
        : this(maximumBytes, cancellationToken, budget, 1024 * 1024) {
    }

    private BoundedPayloadWriter(int maximumBytes, CancellationToken cancellationToken, EventBufferBudget? budget, int admittedMaximumBytes) {
        if (maximumBytes < 0 || maximumBytes > admittedMaximumBytes) {
            throw new ArgumentOutOfRangeException(nameof(maximumBytes), "An upcast output is limited to 1 MiB.");
        }

        _cancellationToken = cancellationToken;
        cancellationToken.ThrowIfCancellationRequested();
        _reservation = (budget ?? new EventBufferBudget(maximumBytes)).Reserve(maximumBytes);
        try {
            _buffer = new byte[maximumBytes];
        }
        catch {
            _reservation.Dispose();
            _reservation = null;
            throw;
        }
    }

    /// <summary>Creates an F output sink under its independently measured V1 readable ceiling.</summary>
    internal static BoundedPayloadWriter CreateLegacy(int maximumBytes, CancellationToken cancellationToken, EventBufferBudget budget)
        => new(maximumBytes, cancellationToken, budget, 64 * 1024 * 1024);

    public void Write(ReadOnlySpan<byte> bytes) {
        lock (_writerLock) {
            ObjectDisposedException.ThrowIf(_buffer is null, this);
            if (_completed) {
                _contractViolated = true;
                throw new InvalidOperationException("The payload writer has already been completed.");
            }

            _cancellationToken.ThrowIfCancellationRequested();
            int nextLength = checked(_length + bytes.Length);
            if (nextLength > _buffer.Length) {
                _contractViolated = true;
                throw new InvalidOperationException("The payload exceeds its reserved output capacity.");
            }

            bytes.CopyTo(_buffer.AsSpan(_length));
            _length = nextLength;
            _cancellationToken.ThrowIfCancellationRequested();
        }
    }

    public void Complete() {
        lock (_writerLock) {
            ObjectDisposedException.ThrowIf(_buffer is null, this);
            _cancellationToken.ThrowIfCancellationRequested();
            if (_completed) {
                _contractViolated = true;
                throw new InvalidOperationException("The payload writer can be completed only once.");
            }

            _completed = true;
        }
    }

    /// <summary>Transfers the sealed owner to a private immutable view.</summary>
    internal ImmutablePayload TakeCompletedPayload() {
        lock (_writerLock) {
            ObjectDisposedException.ThrowIf(_buffer is null, this);
            if (_contractViolated) {
                throw new InvalidOperationException("The output writer recorded a contract violation.");
            }
            if (!_completed) {
                throw new InvalidOperationException("The payload writer must be completed before its output is consumed.");
            }

            if (_transferred) {
                throw new InvalidOperationException("The completed payload owner has already been transferred.");
            }

            _cancellationToken.ThrowIfCancellationRequested();
            byte[] owner = _buffer;
            var payload = new ImmutablePayload(owner, _length, _cancellationToken, _reservation);
            _transferred = true;
            _buffer = null;
            _reservation = null;
            return payload;
        }
    }

    public void Dispose() {
        lock (_writerLock) {
            byte[]? buffer = Interlocked.Exchange(ref _buffer, null);
            if (buffer is not null) {
                CryptographicOperations.ZeroMemory(buffer);
            }

            Interlocked.Exchange(ref _reservation, null)?.Dispose();
        }
    }
}
