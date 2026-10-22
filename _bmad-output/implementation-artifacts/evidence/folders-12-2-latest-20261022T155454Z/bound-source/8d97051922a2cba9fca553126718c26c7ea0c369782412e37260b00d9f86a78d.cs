namespace Hexalith.EventStore.Client.Tests.Streams;

/// <summary>Leaves Task and ValueTask body reads incomplete until explicitly released.</summary>
/// <param name="useTaskRead">Whether the memory overload delegates to the legacy Task overload.</param>
internal sealed class RetainedHistorySuspendedStream(bool useTaskRead) : Stream
{
    private readonly TaskCompletionSource<int> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Optional synchronous invocation or provider-cancellation hook, installed only by focused tests.</summary>
    public Action<CancellationToken>? BeforeReturn { get; init; }

    /// <summary>Signals a pending read before the test advances or cancels the operation.</summary>
    public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Signals that the transport body was safely disposed.</summary>
    public TaskCompletionSource Disposed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Gets the noncooperative pending body read.</summary>
    public Task Pending => _completion.Task;

    /// <summary>Gets the number of actual reads; late completion must not start another.</summary>
    public int ReadCount { get; private set; }

    /// <inheritdoc/>
    public override bool CanRead => true;

    /// <inheritdoc/>
    public override bool CanSeek => false;

    /// <inheritdoc/>
    public override bool CanWrite => false;

    /// <inheritdoc/>
    public override long Length => throw new NotSupportedException();

    /// <inheritdoc/>
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

    /// <summary>Completes with one byte to expose any continuation of the read loop.</summary>
    public void Complete() => _completion.TrySetResult(1);

    /// <inheritdoc/>
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (useTaskRead)
        {
            return base.ReadAsync(buffer, cancellationToken);
        }

        ReadCount++;
        Started.TrySetResult();
        BeforeReturn?.Invoke(cancellationToken);
        return new(_completion.Task);
    }

    /// <inheritdoc/>
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        ReadCount++;
        Started.TrySetResult();
        BeforeReturn?.Invoke(cancellationToken);
        return _completion.Task;
    }

    /// <inheritdoc/>
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void Flush() => throw new NotSupportedException();

    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Disposed.TrySetResult();
        }

        base.Dispose(disposing);
    }
}
