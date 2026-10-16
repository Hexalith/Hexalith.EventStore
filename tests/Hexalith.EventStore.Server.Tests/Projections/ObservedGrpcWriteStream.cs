namespace Hexalith.EventStore.Server.Tests.Projections;

/// <summary>Observes the actual gRPC send-memory segment without copying or retaining its contents.</summary>
internal sealed class ObservedGrpcWriteStream(Action<ReadOnlyMemory<byte>> observe) : Stream
{
    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override void Flush() { }
    public override Task FlushAsync(CancellationToken token) => Task.CompletedTask;
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => observe(buffer.AsMemory(offset, count));
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        observe(buffer);
        return ValueTask.CompletedTask;
    }
}
