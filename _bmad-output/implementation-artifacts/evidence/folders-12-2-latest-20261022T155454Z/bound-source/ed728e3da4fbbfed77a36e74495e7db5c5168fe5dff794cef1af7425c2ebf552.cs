using System.Text;

namespace Hexalith.EventStore.Server.Tests.DomainServices;

/// <summary>Generates a large optional JSON string without allocating the test input.</summary>
internal sealed class OversizedOptionalStateStream(long stateBytes) : Stream
{
    private readonly byte[] _prefix = Encoding.UTF8.GetBytes(
        "{\"events\":[{\"eventTypeName\":\"alias\",\"payload\":\"e30=\"}],\"resultPayload\":\"ok\",\"postCommandStateJson\":\"");
    private readonly byte[] _suffix = "\"}"u8.ToArray();
    private int _prefixOffset;
    private int _suffixOffset;
    private long _remainingStateBytes = stateBytes;

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

    public override int Read(Span<byte> buffer)
    {
        int written = 0;
        if (_prefixOffset < _prefix.Length)
        {
            int take = Math.Min(buffer.Length, _prefix.Length - _prefixOffset);
            _prefix.AsSpan(_prefixOffset, take).CopyTo(buffer);
            _prefixOffset += take;
            written += take;
        }
        if (written < buffer.Length && _remainingStateBytes > 0)
        {
            int take = (int)Math.Min(buffer.Length - written, _remainingStateBytes);
            buffer.Slice(written, take).Fill((byte)'a');
            _remainingStateBytes -= take;
            written += take;
        }
        if (written < buffer.Length && _remainingStateBytes == 0 && _suffixOffset < _suffix.Length)
        {
            int take = Math.Min(buffer.Length - written, _suffix.Length - _suffixOffset);
            _suffix.AsSpan(_suffixOffset, take).CopyTo(buffer[written..]);
            _suffixOffset += take;
            written += take;
        }
        return written;
    }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(Read(buffer.Span));
    }

    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
