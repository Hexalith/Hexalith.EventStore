namespace Hexalith.EventStore.Contracts.Routing;

/// <summary>Restores a caller's decode context after an independent authority operation ends.</summary>
/// <param name="restore">The private caller-context restoration action.</param>
internal sealed class RouteCatalogDecodeSuppression(Action restore) : IDisposable
{
    private Action? _restore = restore;

    /// <summary>Restores the original asynchronous context once without retaining any document bytes.</summary>
    public void Dispose() => Interlocked.Exchange(ref _restore, null)?.Invoke();
}
