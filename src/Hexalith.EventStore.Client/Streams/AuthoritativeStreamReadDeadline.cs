namespace Hexalith.EventStore.Client.Streams;

/// <summary>One monotonic operational budget for gateway invocation, pagination and source evidence release.</summary>
internal sealed class AuthoritativeStreamReadDeadline : IDisposable
{
    private readonly CancellationToken _callerToken;
    private readonly TimeProvider _timeProvider;
    private readonly long _startedAt;
    private readonly TimeSpan _timeout;
    private readonly CancellationTokenSource _timeoutSource;
    private readonly CancellationTokenSource _linkedSource;
    private readonly CancellationTokenSource _providerSource = new();
    private Task? _providerCancellation;

    /// <summary>Starts the remaining timer for one finite budget measured from query entry.</summary>
    public AuthoritativeStreamReadDeadline(TimeSpan timeout, TimeProvider timeProvider, CancellationToken callerToken, long startedAt)
    {
        _callerToken = callerToken;
        _timeProvider = timeProvider;
        _startedAt = startedAt;
        _timeout = timeout;
        TimeSpan remaining = timeout - timeProvider.GetElapsedTime(startedAt);
        _timeoutSource = new(remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero, timeProvider);
        _linkedSource = CancellationTokenSource.CreateLinkedTokenSource(callerToken, _timeoutSource.Token);
    }

    /// <summary>Distinguishes the actual whole-read budget from a provider's independent cancellation.</summary>
    public bool IsExpired => _timeoutSource.IsCancellationRequested || _timeProvider.GetElapsedTime(_startedAt) >= _timeout;

    /// <summary>Rejects caller cancellation first, then elapsed or timer-expired deadlines.</summary>
    public void ThrowIfCancellationRequested()
    {
        if (_callerToken.IsCancellationRequested)
        {
            CancelProvider();
        }

        _callerToken.ThrowIfCancellationRequested();
        if (_timeProvider.GetElapsedTime(_startedAt) >= _timeout)
        {
            _timeoutSource.Cancel();
        }

        if (_linkedSource.IsCancellationRequested)
        {
            CancelProvider();
        }

        _linkedSource.Token.ThrowIfCancellationRequested();
    }

    /// <summary>Bounds both synchronous provider invocation and its noncooperative returned task.</summary>
    public async Task<T> ReadAsync<T>(Func<CancellationToken, Task<T>> read, Action<Task<T>>? operationStarted = null, Action<T>? abandonedResultCleanup = null)
    {
        ThrowIfCancellationRequested();
        CancellationToken token = _providerSource.Token;
        Task<T> pending = Task.Run(() => read(token), token);
        // A provider can fault after the caller has stopped waiting. Observe that fault
        // without ever resuming the stream read or releasing its result.
        _ = pending.ContinueWith(static task => { _ = task.Exception; }, CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        operationStarted?.Invoke(pending);
        try
        {
            // The query's private token has no provider callbacks. A provider may block
            // indefinitely in its own cancellation callback without delaying this wait.
            T result = await pending.WaitAsync(_linkedSource.Token).ConfigureAwait(false);
            ThrowIfCancellationRequested();
            return result;
        }
        catch
        {
            if (abandonedResultCleanup is not null)
            {
                // Ownership transfers only when the provider terminates. One continuation also covers
                // completion followed by a failed terminal deadline check; no result was returned.
                _ = pending.ContinueWith(task =>
                {
                    try { if (task.IsCompletedSuccessfully) { abandonedResultCleanup(task.Result); } }
                    catch (Exception) { /* Cleanup cannot resume or release abandoned evidence. */ }
                }, CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
            }
            throw;
        }
        finally
        {
            if (_linkedSource.IsCancellationRequested)
            {
                CancelProvider();
            }
        }
    }

    private void CancelProvider()
    {
        if (_providerCancellation is not null)
        {
            return;
        }

        // CancelAsync signals the token immediately and runs provider callbacks away
        // from the query continuation. Callback failures must never release evidence.
        _providerCancellation = _providerSource.CancelAsync();
        _ = _providerCancellation.ContinueWith(static task => { _ = task.Exception; }, CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _linkedSource.Dispose();
        _timeoutSource.Dispose();
        if (_providerCancellation is { IsCompleted: false } cancellation)
        {
            // Cleanup also stays independent of callbacks that have not returned yet.
            _ = cancellation.ContinueWith(static (_, state) => ((CancellationTokenSource)state!).Dispose(),
                _providerSource, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }
        else
        {
            _providerSource.Dispose();
        }
    }
}
