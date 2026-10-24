// Effective authority: 08bfd5855998a276999db38447c409b2231f3d4e253d4e371d4d8bfe369462d6; amendment sections 3-4; base sections 10.5-10.7 and 15.
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>Tracks a local operation until its owned buffers and any abandoned backend result have been cleared.</summary>
internal sealed class PayloadLocalOperation(CancellationToken cancellationToken, Action<PayloadLocalOperation> released) : IDisposable
{
    private readonly CancellationTokenSource _cancel = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    private readonly TaskCompletionSource _cleared = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _references = 1;
    private int _disposed;

    /// <summary>Gets the token cancelled by caller cancellation or a local lifecycle drain.</summary>
    internal CancellationToken Token => _cancel.Token;
    /// <summary>Gets completion only after every registered late result has been disposed.</summary>
    internal Task Cleared => _cleared.Task;
    /// <summary>Rejects results from cancelled or already-cleared work.</summary>
    internal void AssertCurrent(CancellationToken caller = default)
    {
        caller.ThrowIfCancellationRequested();
        if (_cancel.IsCancellationRequested || Volatile.Read(ref _disposed) != 0)
        {
            throw new PayloadProtectionStateException(UnreadableProtectedDataReason.KeyInvalidatedOrDeleted);
        }
    }
    /// <summary>Requests cancellation; a failed callback cannot establish clearing evidence.</summary>
    internal void Cancel()
    {
        try { _cancel.Cancel(); }
        catch (AggregateException) { /* Wait for actual disposal even when a cancellation observer fails. */ }
        catch (ObjectDisposedException) { /* An operation that finished after the drain snapshot already cleared. */ }
    }
    /// <summary>Extends clearing evidence through a background result's final disposal.</summary>
    internal void RetainUntil(Task clearing)
    {
        Interlocked.Increment(ref _references);
        _ = ReleaseAfterAsync(clearing);
    }
    /// <summary>Signals that the foreground owner has cleared its buffers.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0) { Release(); }
    }
    private async Task ReleaseAfterAsync(Task clearing)
    {
        try { await clearing.ConfigureAwait(false); }
        catch { /* Observe failure without exposing private backend detail. */ }
        finally { Release(); }
    }
    private void Release()
    {
        if (Interlocked.Decrement(ref _references) != 0) { return; }
        released(this);
        _cancel.Dispose();
        _cleared.TrySetResult();
    }
}
