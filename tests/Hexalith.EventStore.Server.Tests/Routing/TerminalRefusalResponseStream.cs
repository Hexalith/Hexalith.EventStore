namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Holds response execution and optionally fails after the configured number of successful bounded writes.</summary>
internal sealed class TerminalRefusalResponseStream(Stream target, bool fail, int successfulWritesBeforeFailure = 0) : Stream
{
    internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal int SuccessfulWrites { get; private set; }
    internal long WrittenBytes { get; private set; }

    /// <inheritdoc/>
    public override bool CanRead => false;
    /// <inheritdoc/>
    public override bool CanSeek => false;
    /// <inheritdoc/>
    public override bool CanWrite => true;
    /// <inheritdoc/>
    public override long Length => throw new NotSupportedException();
    /// <inheritdoc/>
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    /// <inheritdoc/>
    public override void Flush() => target.Flush();
    /// <inheritdoc/>
    public override Task FlushAsync(CancellationToken cancellationToken) => target.FlushAsync(cancellationToken);
    /// <inheritdoc/>
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    /// <inheritdoc/>
    public override void SetLength(long value) => throw new NotSupportedException();
    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    /// <inheritdoc/>
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        => WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    /// <inheritdoc/>
    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        Started.TrySetResult();
        await Release.Task;
        cancellationToken.ThrowIfCancellationRequested();
        if (fail && SuccessfulWrites >= successfulWritesBeforeFailure) throw new IOException("Response write unavailable.");
        await target.WriteAsync(buffer, cancellationToken);
        SuccessfulWrites++;
        WrittenBytes += buffer.Length;
        if (successfulWritesBeforeFailure > 0) await target.FlushAsync(cancellationToken);
    }
}
