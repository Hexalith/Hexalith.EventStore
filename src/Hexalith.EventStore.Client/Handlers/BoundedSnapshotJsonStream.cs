namespace Hexalith.EventStore.Client.Handlers;

/// <summary>Stops a qualified folded-state serializer at its pre-admitted byte ceiling.</summary>
internal sealed class BoundedSnapshotJsonStream : Stream
{
    private readonly MemoryStream _buffer;
    private readonly int _maximumBytes;

    internal BoundedSnapshotJsonStream(int maximumBytes)
    {
        _maximumBytes = maximumBytes;
        _buffer = new MemoryStream(maximumBytes);
    }

    internal byte[] ToArray() => _buffer.ToArray();

    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => _buffer.Length;
    public override long Position { get => _buffer.Position; set => throw new NotSupportedException(); }
    public override void Flush() => _buffer.Flush();
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count)
    {
        if (count > _maximumBytes - _buffer.Length) { throw new InvalidOperationException("SnapshotCaptureLimitExceeded"); }
        _buffer.Write(buffer, offset, count);
    }
    public override void Write(ReadOnlySpan<byte> buffer)
    {
        if (buffer.Length > _maximumBytes - _buffer.Length) { throw new InvalidOperationException("SnapshotCaptureLimitExceeded"); }
        _buffer.Write(buffer);
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) { _buffer.Dispose(); }
        base.Dispose(disposing);
    }
}
