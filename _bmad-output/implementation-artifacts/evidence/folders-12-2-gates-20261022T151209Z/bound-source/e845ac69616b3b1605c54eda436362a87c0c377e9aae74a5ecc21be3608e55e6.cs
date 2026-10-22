namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

/// <summary>Counts actual fixture HTTP body bytes while forwarding without buffering or retaining content.</summary>
internal sealed class LiveByteCountingStream(Stream inner) : Stream
{
    internal long Bytes { get; private set; }
    public override bool CanRead => inner.CanRead;
    public override bool CanSeek => false;
    public override bool CanWrite => inner.CanWrite;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override void Flush() => inner.Flush();
    public override Task FlushAsync(CancellationToken token) => inner.FlushAsync(token);
    public override int Read(byte[] buffer, int offset, int count)
    { int read = inner.Read(buffer, offset, count); Bytes += read; return read; }
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token = default)
    { int read = await inner.ReadAsync(buffer, token).ConfigureAwait(false); Bytes += read; return read; }
    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken token)
    { int read = await inner.ReadAsync(buffer.AsMemory(offset, count), token).ConfigureAwait(false); Bytes += read; return read; }
    public override void Write(byte[] buffer, int offset, int count)
    { inner.Write(buffer, offset, count); Bytes += count; }
    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken token = default)
    { await inner.WriteAsync(buffer, token).ConfigureAwait(false); Bytes += buffer.Length; }
    public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken token)
    { await inner.WriteAsync(buffer.AsMemory(offset, count), token).ConfigureAwait(false); Bytes += count; }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
}
