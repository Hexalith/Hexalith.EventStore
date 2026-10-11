namespace Hexalith.EventStore.Server.Events;

/// <summary>Admits snapshot serialization before the serializer can grow its output buffer.</summary>
internal sealed class BoundedSnapshotByteStream(int maximumBytes) : Stream
{
    private readonly MemoryStream _buffer = new(maximumBytes);

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
        if (count > maximumBytes - _buffer.Length) { throw new InvalidOperationException("SnapshotCaptureLimitExceeded"); }
        _buffer.Write(buffer, offset, count);
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        if (buffer.Length > maximumBytes - _buffer.Length) { throw new InvalidOperationException("SnapshotCaptureLimitExceeded"); }
        _buffer.Write(buffer);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { _buffer.Dispose(); }
        base.Dispose(disposing);
    }
}
