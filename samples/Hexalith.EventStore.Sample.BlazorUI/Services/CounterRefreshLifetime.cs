namespace Hexalith.EventStore.Sample.BlazorUI.Services;

/// <summary>Serializes refresh work on a Blazor renderer and cancels it when the component is disposed.</summary>
internal sealed class CounterRefreshLifetime : IDisposable
{
    private readonly CancellationTokenSource _stop = new();
    private readonly CancellationToken _token;
    private Func<CancellationToken, Task>? _pending;
    private bool _refreshing;

    /// <summary>Initializes the refresh lifetime.</summary>
    internal CounterRefreshLifetime() => _token = _stop.Token;

    /// <summary>Gets whether the component has stopped accepting refresh work.</summary>
    internal bool IsDisposed => _token.IsCancellationRequested;

    /// <summary>Runs refresh work, keeping at most one pending refresh while a request is active.</summary>
    /// <param name="refresh">The work to run with the component lifetime token.</param>
    /// <returns>The task for the active refresh loop, or an already completed task if the request was queued.</returns>
    /// <remarks>Call on the component's renderer synchronization context.</remarks>
    internal async Task RunAsync(Func<CancellationToken, Task> refresh)
    {
        if (IsDisposed)
        {
            return;
        }

        _pending = refresh;
        if (_refreshing)
        {
            return;
        }

        _refreshing = true;
        try
        {
            while (!IsDisposed && _pending is { } next)
            {
                _pending = null;
                await next(_token).ConfigureAwait(true);
            }
        }
        catch (OperationCanceledException) when (IsDisposed)
        {
        }
        finally
        {
            _refreshing = false;
        }
    }

    /// <summary>Cancels in-flight work and discards any queued refresh.</summary>
    public void Dispose()
    {
        if (IsDisposed)
        {
            return;
        }

        _pending = null;
        try
        {
            _stop.Cancel();
        }
        finally
        {
            _stop.Dispose();
        }
    }
}
