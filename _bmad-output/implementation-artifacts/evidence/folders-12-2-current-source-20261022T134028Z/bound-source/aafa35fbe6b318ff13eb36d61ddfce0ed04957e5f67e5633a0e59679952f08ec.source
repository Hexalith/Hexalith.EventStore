using System.Net;

namespace Hexalith.EventStore.Client.Tests.Streams;

/// <summary>Holds stream acquisition open regardless of the caller or deadline cancellation token.</summary>
/// <param name="suspendAcquisition">Whether acquiring the body remains pending.</param>
/// <param name="stream">The transport-owned body stream.</param>
internal sealed class RetainedHistorySuspendedContent(bool suspendAcquisition, Stream stream) : HttpContent
{
    private readonly TaskCompletionSource<Stream> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Optional synchronous invocation or provider-cancellation hook, installed only by focused tests.</summary>
    public Action<CancellationToken>? BeforeReturn { get; init; }

    /// <summary>Optional blocking or failing provider disposal, installed only by focused tests.</summary>
    public Action? BeforeDispose { get; init; }

    /// <summary>Signals that the SDK attempted to acquire the body.</summary>
    public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Signals disposal of the response content.</summary>
    public TaskCompletionSource Disposed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Gets the still-pending body acquisition.</summary>
    public Task Pending => _completion.Task;

    /// <summary>Gets the number of body acquisitions, including any forbidden late continuation.</summary>
    public int AcquisitionCount { get; private set; }

    /// <summary>Returns the late stream after the bounded read has already stopped.</summary>
    public void Complete() => _completion.TrySetResult(stream);

    /// <inheritdoc/>
    protected override Task<Stream> CreateContentReadStreamAsync(CancellationToken cancellationToken)
    {
        AcquisitionCount++;
        Started.TrySetResult();
        BeforeReturn?.Invoke(cancellationToken);
        return suspendAcquisition ? _completion.Task : Task.FromResult(stream);
    }

    /// <inheritdoc/>
    protected override Task<Stream> CreateContentReadStreamAsync() => CreateContentReadStreamAsync(CancellationToken.None);

    /// <inheritdoc/>
    protected override Task SerializeToStreamAsync(Stream target, TransportContext? context) => throw new NotSupportedException();

    /// <inheritdoc/>
    protected override bool TryComputeLength(out long length)
    {
        length = 0;
        return false;
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        try
        {
            if (disposing) { BeforeDispose?.Invoke(); }
        }
        finally
        {
            base.Dispose(disposing);
            if (disposing) { Disposed.TrySetResult(); }
        }
    }
}
