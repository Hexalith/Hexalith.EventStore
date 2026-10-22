namespace Hexalith.EventStore.Server.Tests.Authentication;

/// <summary>A nonseekable synthetic one-MiB response with observable consumption and no announced Content-Length.</summary>
internal sealed class SyntheticOversizedTokenStream : Stream
{
    /// <summary>Gets bytes actually read by the client.</summary>
    public long BytesRead { get; private set; }
    /// <inheritdoc/>
    public override bool CanRead => true;
    /// <inheritdoc/>
    public override bool CanSeek => false;
    /// <inheritdoc/>
    public override bool CanWrite => false;
    /// <inheritdoc/>
    public override long Length => throw new NotSupportedException();
    /// <inheritdoc/>
    public override long Position { get => BytesRead; set => throw new NotSupportedException(); }
    /// <inheritdoc/>
    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));
    /// <inheritdoc/>
    public override int Read(Span<byte> buffer)
    {
        int count = (int)Math.Min(buffer.Length, 1024 * 1024 - BytesRead);
        buffer[..count].Fill((byte)'x');
        BytesRead += count;
        return count;
    }
    /// <inheritdoc/>
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(Read(buffer.Span));
    }
    /// <inheritdoc/>
    public override void Flush() { }
    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    /// <inheritdoc/>
    public override void SetLength(long value) => throw new NotSupportedException();
    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
