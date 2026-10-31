using Hexalith.EventStore.Contracts.Routing;

namespace Hexalith.EventStore.ServiceDefaults.Routing;

/// <summary>Tracks demonstrated remote ambiguity for the active projection scopes, including their private helpers.</summary>
public static class RouteCatalogRemoteCall
{
    private static readonly AsyncLocal<IReadOnlyList<RouteCatalogDispatchLease>?> Active = new();
    internal static IReadOnlyList<RouteCatalogDispatchLease> Current => Active.Value ?? [];
    /// <summary>Enters a receiver execution scope; disposal restores its enclosing scope without releasing the lease.</summary>
    public static IDisposable Enter(RouteCatalogDispatchLease lease)
    {
        ArgumentNullException.ThrowIfNull(lease);
        return new RouteCatalogRemoteScope(lease);
    }
    /// <summary>Captures the prior scope and enrolls this lease for actual remote calls.</summary>
    internal static IReadOnlyList<RouteCatalogDispatchLease>? Push(RouteCatalogDispatchLease? lease, bool isolated = false)
    {
        var prior = Active.Value;
        if (lease is not null) Active.Value = isolated ? [lease] : [.. prior ?? [], lease];
        RouteCatalogRemoteExecution.Current = Active.Value is { } leases ? new RouteCatalogRemoteBoundary(leases) : null;
        return prior;
    }
    /// <summary>Restores the enclosing dispatch scope.</summary>
    internal static void Restore(IReadOnlyList<RouteCatalogDispatchLease>? prior)
    {
        Active.Value = prior;
        RouteCatalogRemoteExecution.Current = prior is null ? null : new RouteCatalogRemoteBoundary(prior);
    }

    /// <summary>Awaits one actual remote call; a failed or abandoned await retains its active durable fences.</summary>
    public static Task<T> InvokeAsync<T>(Func<Task<T>> invoke) => RouteCatalogRemoteExecution.InvokeAsync(invoke);

    /// <summary>Awaits one actual remote call without a return value.</summary>
    public static Task InvokeAsync(Func<Task> invoke) => RouteCatalogRemoteExecution.InvokeAsync(invoke);

    /// <summary>Refuses cancellation before dispatch, then tracks one actual remote call.</summary>
    public static Task<T> InvokeAsync<T>(Func<Task<T>> invoke, CancellationToken token) => RouteCatalogRemoteExecution.InvokeAsync(invoke, token);

    /// <summary>Refuses cancellation before dispatch, then tracks one actual void remote call.</summary>
    public static Task InvokeAsync(Func<Task> invoke, CancellationToken token) => RouteCatalogRemoteExecution.InvokeAsync(invoke, token);

    /// <summary>Tracks an RPC on a client already created and validated locally, before remote enrollment begins.</summary>
    public static Task<TResult> InvokeAsync<TClient, TResult>(TClient client, Func<TClient, Task<TResult>> invoke)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(invoke);
        return RouteCatalogRemoteExecution.InvokeAsync(() => invoke(client));
    }

    /// <summary>Tracks a void RPC on an already locally created client.</summary>
    public static Task InvokeAsync<TClient>(TClient client, Func<TClient, Task> invoke)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(invoke);
        return RouteCatalogRemoteExecution.InvokeAsync(() => invoke(client));
    }
}
