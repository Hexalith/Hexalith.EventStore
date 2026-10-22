using System.Text.Json;

using Hexalith.EventStore.Client.Handlers;

namespace Hexalith.EventStore.Server.Events;

/// <summary>Admits snapshot serialization before the serializer can grow its output buffer.</summary>
internal sealed class BoundedSnapshotByteStream : Stream
{
    private readonly MemoryStream _buffer;
    private readonly int _maximumBytes;

    /// <summary>Admits serializer scratch before allocating the canonical output buffer.</summary>
    internal BoundedSnapshotByteStream(int maximumBytes, JsonSerializerOptions options, long scratchBytes, long maximumInputBytes)
    {
        if (!BoundedSnapshotJsonBuffer.CanSerialize(maximumBytes, options, scratchBytes, maximumInputBytes))
        {
            throw new InvalidOperationException("SnapshotCaptureLimitExceeded");
        }
        _maximumBytes = maximumBytes;
        _buffer = new MemoryStream(maximumBytes);
    }

    /// <summary>Gets the admitted canonical output ceiling.</summary>
    internal int CapacityLimit => _maximumBytes;

    /// <summary>Gets canonical output bytes without allocating another measurement copy.</summary>
    internal ReadOnlySpan<byte> WrittenSpan => _buffer.GetBuffer().AsSpan(0, checked((int)_buffer.Length));

    /// <summary>Copies canonical bytes into their independently admitted measurement buffer.</summary>
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
