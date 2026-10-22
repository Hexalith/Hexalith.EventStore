using System.Security.Cryptography;

using Hexalith.EventStore.Contracts.Projections;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>Computes an exact JSON byte count and digest without retaining the replay body.</summary>
internal sealed class ProjectionRebuildHashCountStream : Stream
{
    private readonly IncrementalHash _hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    private long _length;

    internal string Digest => Convert.ToHexString(_hash.GetHashAndReset());
    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => _length;
    public override long Position { get => _length; set => throw new NotSupportedException(); }

    public override void Write(byte[] buffer, int offset, int count)
    {
        _hash.AppendData(buffer.AsSpan(offset, count));
        _length = checked(_length + count);
    }

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _hash.AppendData(buffer.Span);
        _length = checked(_length + buffer.Length);
        return ValueTask.CompletedTask;
    }

    public override void Flush() { }
    public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing) _hash.Dispose();
        base.Dispose(disposing);
    }
}

/// <summary>Streams deterministic replay JSON into bounded protected-route uploads.</summary>
internal sealed class ProjectionRebuildPartUploadStream(
    Func<int, byte[], CancellationToken, Task> upload) : Stream
{
    private readonly byte[] _buffer = new byte[ProjectionRebuildTransportReference.PartBytes];
    private readonly IncrementalHash _hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    private int _used;
    private int _partCount;
    private long _length;

    internal int PartCount => _partCount;
    internal long WrittenLength => _length;
    internal string Digest => Convert.ToHexString(_hash.GetHashAndReset());
    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> source,
        CancellationToken cancellationToken = default)
    {
        while (!source.IsEmpty)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int count = Math.Min(source.Length, _buffer.Length - _used);
            source[..count].CopyTo(_buffer.AsMemory(_used));
            _hash.AppendData(source.Span[..count]);
            _length = checked(_length + count);
            source = source[count..];
            _used += count;
            if (_used == _buffer.Length)
                await SendAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    internal Task CompleteAsync(CancellationToken cancellationToken)
        => _used == 0 ? Task.CompletedTask : SendAsync(cancellationToken);

    private async Task SendAsync(CancellationToken cancellationToken)
    {
        byte[] data = _buffer.AsSpan(0, _used).ToArray();
        await upload(_partCount, data, cancellationToken).ConfigureAwait(false);
        _partCount++;
        _used = 0;
    }

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override void Flush() { }
    public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing) _hash.Dispose();
        base.Dispose(disposing);
    }
}
