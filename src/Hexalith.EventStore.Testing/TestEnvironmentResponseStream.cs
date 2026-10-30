namespace Hexalith.EventStore.Testing;

/// <summary>Applies fixture cancellation to response reads even when callers omit their own token.</summary>
/// <param name="stream">The transport-owned response stream.</param>
/// <param name="lifetime">The original bounded request and fixture lifetime.</param>
internal sealed class TestEnvironmentResponseStream(Stream stream, CancellationToken lifetime) : Stream
{
    /// <inheritdoc/>
    public override bool CanRead => stream.CanRead;
    /// <inheritdoc/>
    public override bool CanSeek => stream.CanSeek;
    /// <inheritdoc/>
    public override bool CanWrite => false;
    /// <inheritdoc/>
    public override long Length => stream.Length;
    /// <inheritdoc/>
    public override long Position { get => stream.Position; set => stream.Position = value; }
    /// <inheritdoc/>
    public override void Flush() => stream.Flush();
    /// <inheritdoc/>
    public override int Read(byte[] buffer, int offset, int count)
        => ReadAsync(buffer.AsMemory(offset, count), CancellationToken.None).AsTask().GetAwaiter().GetResult();
    /// <inheritdoc/>
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        => ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    /// <inheritdoc/>
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        lifetime.ThrowIfCancellationRequested();
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(lifetime, cancellationToken);
        return await stream.ReadAsync(buffer, linked.Token).ConfigureAwait(false);
    }
    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin) => stream.Seek(offset, origin);
    /// <inheritdoc/>
    public override void SetLength(long value) => throw new NotSupportedException();
    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing) stream.Dispose();
        base.Dispose(disposing);
    }
}
