namespace Hexalith.EventStore.Contracts.Routing;

/// <summary>Shared actual-RPC boundary used by platform transports and custom handlers performing remote work.</summary>
public static class RouteCatalogRemoteExecution
{
    private static readonly AsyncLocal<IRouteCatalogRemoteBoundary?> Active = new();

    /// <summary>Gets or sets the ambient hosting boundary. Hosts save and restore this value for each execution scope.</summary>
    public static IRouteCatalogRemoteBoundary? Current
    {
        get => Active.Value;
        set => Active.Value = value;
    }

    /// <summary>
    /// Enrolls before starting actual remote work and retains its durable fences on transport failure or cancellation.
    /// A token already cancelled on entry throws before enrollment, because no remote work has started.
    /// </summary>
    public static async Task<T> InvokeAsync<T>(Func<Task<T>> invoke, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(invoke);
        cancellationToken.ThrowIfCancellationRequested();
        IRouteCatalogRemoteBoundary? boundary = Current;
        boundary?.Enroll();
        try { return await invoke().ConfigureAwait(false); }
        catch { boundary?.Retain(); throw; }
        finally { boundary?.Complete(); }
    }

    /// <summary>Tracks actual remote work without a return value.</summary>
    public static async Task InvokeAsync(Func<Task> invoke, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(invoke);
        cancellationToken.ThrowIfCancellationRequested();
        IRouteCatalogRemoteBoundary? boundary = Current;
        boundary?.Enroll();
        try { await invoke().ConfigureAwait(false); }
        catch { boundary?.Retain(); throw; }
        finally { boundary?.Complete(); }
    }
}
