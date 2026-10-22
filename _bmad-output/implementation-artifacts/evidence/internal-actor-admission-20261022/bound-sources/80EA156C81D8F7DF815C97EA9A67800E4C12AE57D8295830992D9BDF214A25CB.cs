namespace Hexalith.EventStore.Server.DomainServices;

/// <summary>Counts every received response byte and limits each transport read to 64 KiB.</summary>
/// <remarks>This transport boundary does not attest JSON materialization or composed parser workspace.</remarks>
internal sealed class BoundedDomainServiceResponseStream : Stream
{
    private readonly Stream _source;
    private readonly long _maximumBytes;
    private readonly CancellationToken _operationCancellation;
    private long _received;
    private bool _disposed;

    /// <summary>Owns a readable HTTP response stream under the selected whole-result limit.</summary>
    internal BoundedDomainServiceResponseStream(Stream source, long maximumBytes, CancellationToken operationCancellation)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!source.CanRead || maximumBytes is < 1 or > 128L * 1024 * 1024)
        {
            throw new ArgumentException("ResultLimit: invalid bounded response stream.");
        }

        _source = source;
        _maximumBytes = maximumBytes;
        _operationCancellation = operationCancellation;
    }

    /// <inheritdoc/>
    public override bool CanRead => !_disposed;
    /// <inheritdoc/>
    public override bool CanSeek => false;
    /// <inheritdoc/>
    public override bool CanWrite => false;
    /// <inheritdoc/>
    public override long Length => throw new NotSupportedException();
    /// <inheritdoc/>
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    /// <inheritdoc/>
    public override void Flush() => throw new NotSupportedException();
    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    /// <inheritdoc/>
    public override void SetLength(long value) => throw new NotSupportedException();
    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    /// <inheritdoc/>
    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));
    /// <inheritdoc/>
    public override int Read(Span<byte> buffer)
    {
        int limit = SelectReadLength(buffer.Length);
        int read = _source.Read(buffer[..limit]);
        Accept(read);
        return read;
    }
    /// <inheritdoc/>
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        int limit = SelectReadLength(buffer.Length);
        cancellationToken.ThrowIfCancellationRequested();
        int read;
        if (!cancellationToken.CanBeCanceled || cancellationToken == _operationCancellation)
        {
            read = await _source.ReadAsync(buffer[..limit], _operationCancellation).ConfigureAwait(false);
        }
        else
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _operationCancellation);
            read = await _source.ReadAsync(buffer[..limit], linked.Token).ConfigureAwait(false);
        }

        Accept(read);
        return read;
    }
    /// <inheritdoc/>
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        => ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            _disposed = true;
            if (disposing) { _source.Dispose(); }
        }

        base.Dispose(disposing);
    }

    /// <summary>Admits at most one byte past the cap solely to detect an oversized stream.</summary>
    private int SelectReadLength(int requested)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _operationCancellation.ThrowIfCancellationRequested();
        return checked((int)Math.Min(requested, Math.Min(64 * 1024L, _maximumBytes - _received + 1)));
    }

    /// <summary>Refuses overflow/cancellation without exposing a successful typed result.</summary>
    private void Accept(int read)
    {
        _operationCancellation.ThrowIfCancellationRequested();
        _received = checked(_received + read);
        if (_received > _maximumBytes)
        {
            Dispose();
            throw new InvalidOperationException("ResultLimit: complete received domain result exceeds the selected cap.");
        }
    }
}
