// Effective authority: eb45653553efe53ba83e6dd6c9796b47dfb6fb0bba0628ff95f6bc1b96b054c9; amendment sections 3-4; base sections 10.5-10.7 and 15.
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>Tracks a local operation until its owned buffers and any abandoned backend result have been cleared.</summary>
internal sealed class PayloadLocalOperation : IDisposable
{
    private readonly CancellationToken _callerToken;
    private readonly CancellationTokenRegistration _parentCompletion;
    private readonly CancellationTokenSource _cancel;
    private readonly Action<PayloadLocalOperation> _released;
    private readonly TaskCompletionSource _cleared = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _parentCallbacksCompleted = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly object _gate = new();
    private Task? _cancellation;
    private int _references = 1;
    private int _disposed;

    /// <summary>Links caller cancellation while retaining evidence that its callbacks have returned.</summary>
    internal PayloadLocalOperation(CancellationToken cancellationToken, Action<PayloadLocalOperation> released)
    {
        _callerToken = cancellationToken;
        _released = released;
        // Registrations execute in reverse order: this sentinel runs after the linked source's
        // parent callback, including any operation-token callback that it invokes.
        _parentCompletion = cancellationToken.UnsafeRegister(static state => ((PayloadLocalOperation)state!)._parentCallbacksCompleted.TrySetResult(), this);
        _cancel = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    }

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
    /// <summary>Starts one asynchronous cancellation and retains callback completion as part of clearing evidence.</summary>
    internal Task CancelAsync()
    {
        lock (_gate)
        {
            if (_cancellation is not null) { return _cancellation; }
            if (_references == 0) { return Task.CompletedTask; }
            _references++;
            return _cancellation = Task.Run(CancelCoreAsync);
        }
    }
    private async Task CancelCoreAsync()
    {
        try { await _cancel.CancelAsync().ConfigureAwait(false); }
        catch (AggregateException) { /* Callbacks have finished; actual foreground/result clearing is still required. */ }
        finally
        {
            if (_callerToken.IsCancellationRequested) { await _parentCallbacksCompleted.Task.ConfigureAwait(false); }
            Release();
        }
    }
    /// <summary>Extends clearing evidence through a background result's final disposal.</summary>
    internal void RetainUntil(Task clearing)
    {
        lock (_gate)
        {
            if (_references == 0) { throw new ObjectDisposedException(nameof(PayloadLocalOperation)); }
            _references++;
        }
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
        lock (_gate)
        {
            if (--_references != 0) { return; }
        }
        _ = Task.Run(CompleteReleaseAsync);
    }

    private async Task CompleteReleaseAsync()
    {
        if (_callerToken.IsCancellationRequested) { await _parentCallbacksCompleted.Task.ConfigureAwait(false); }
        _cancel.Dispose();
        _parentCompletion.Dispose();
        _cleared.TrySetResult();
        _released(this);
    }
}
