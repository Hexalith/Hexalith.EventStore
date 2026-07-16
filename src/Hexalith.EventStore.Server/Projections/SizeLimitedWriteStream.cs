namespace Hexalith.EventStore.Server.Projections;

/// <summary>Counts serialized bytes without retaining them and fails as soon as a limit is exceeded.</summary>
internal sealed class SizeLimitedWriteStream(long maxBytes) : Stream {
    private long _bytesWritten;

    public override bool CanRead => false;

    public override bool CanSeek => false;

    public override bool CanWrite => true;

    public override long Length => _bytesWritten;

    public override long Position {
        get => _bytesWritten;
        set => throw new NotSupportedException();
    }

    public override void Flush() {
    }

    public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => Add(count);

    public override void Write(ReadOnlySpan<byte> buffer) => Add(buffer.Length);

    public override ValueTask WriteAsync(
        ReadOnlyMemory<byte> buffer,
        CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        Add(buffer.Length);
        return ValueTask.CompletedTask;
    }

    private void Add(int count) {
        if (count < 0 || _bytesWritten > maxBytes - count) {
            throw new ProjectionRebuildPrefixSafetyLimitExceededException();
        }

        _bytesWritten += count;
    }
}
