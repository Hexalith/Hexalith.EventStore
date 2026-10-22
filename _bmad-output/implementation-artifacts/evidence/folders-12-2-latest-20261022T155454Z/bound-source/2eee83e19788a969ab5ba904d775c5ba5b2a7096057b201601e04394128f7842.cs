namespace Hexalith.EventStore.Server.Projections;

/// <summary>Starts one shared finalization deadline only after read-only verification, or when failed-work cleanup begins.</summary>
internal sealed class ProjectionSourceGuardFinalizationBudget(TimeProvider clock, Action? started = null) : IDisposable
{
    private CancellationTokenSource? _deadline;

    /// <summary>Returns the original bounded token, starting its deadline at the first finalization operation.</summary>
    internal CancellationToken Begin()
    {
        if (_deadline is null)
        {
            started?.Invoke();
            _deadline = new(TimeSpan.FromSeconds(30), clock);
        }
        return _deadline.Token;
    }

    /// <inheritdoc/>
    public void Dispose() => _deadline?.Dispose();
}
