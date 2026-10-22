using System.Security.Cryptography;

namespace Hexalith.EventStore.DomainService;

/// <summary>Enforces a declared serializer bound without exposing mutable private storage.</summary>
internal sealed class BoundedV1PayloadStream : Stream
{
    private readonly object _gate = new();
    private readonly CancellationToken _operationCancellation;
    private byte[]? _owner;
    private int _length;
    private bool _sealed;
    private bool _violation;

    /// <summary>Reserves exact declared capacity before the serializer is invoked.</summary>
    internal BoundedV1PayloadStream(int maximumBytes, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumBytes);
        cancellationToken.ThrowIfCancellationRequested();
        _operationCancellation = cancellationToken;
        _owner = new byte[maximumBytes];
    }

    /// <summary>Seals the callback lease and returns a detached bounded transport payload.</summary>
    internal byte[] SealAndCopy()
    {
        lock (_gate)
        {
            _operationCancellation.ThrowIfCancellationRequested();
            ObjectDisposedException.ThrowIf(_owner is null, this);
            if (_sealed || _violation)
            {
                throw new InvalidOperationException("PayloadLimit: the bounded serializer violated its output contract.");
            }

            _sealed = true;
            return _owner.AsSpan(0, _length).ToArray();
        }
    }

    /// <inheritdoc/>
    public override bool CanRead => false;
    /// <inheritdoc/>
    public override bool CanSeek => false;
    /// <inheritdoc/>
    public override bool CanWrite => !_sealed && _owner is not null;
    /// <inheritdoc/>
    public override long Length => _length;
    /// <inheritdoc/>
    public override long Position { get => _length; set => throw new NotSupportedException(); }
    /// <inheritdoc/>
    public override void Flush() => _operationCancellation.ThrowIfCancellationRequested();
    /// <inheritdoc/>
    public override Task FlushAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Flush();
        return Task.CompletedTask;
    }
    /// <inheritdoc/>
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    /// <inheritdoc/>
    public override void SetLength(long value) => throw new NotSupportedException();
    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));
    /// <inheritdoc/>
    public override void Write(ReadOnlySpan<byte> buffer)
    {
        lock (_gate)
        {
            _operationCancellation.ThrowIfCancellationRequested();
            ObjectDisposedException.ThrowIf(_owner is null, this);
            if (_sealed || buffer.Length > _owner.Length - _length)
            {
                _violation = true;
                throw new InvalidOperationException("PayloadLimit: the serializer exceeded or wrote after its bounded output.");
            }

            buffer.CopyTo(_owner.AsSpan(_length));
            _length += buffer.Length;
        }
    }
    /// <inheritdoc/>
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Write(buffer.Span);
        return ValueTask.CompletedTask;
    }
    /// <inheritdoc/>
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Write(buffer, offset, count);
        return Task.CompletedTask;
    }
    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        lock (_gate)
        {
            if (_owner is not null)
            {
                CryptographicOperations.ZeroMemory(_owner);
                _owner = null;
            }
        }

        base.Dispose(disposing);
    }
}
