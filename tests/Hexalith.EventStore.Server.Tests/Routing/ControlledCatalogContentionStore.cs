using Hexalith.EventStore.ServiceDefaults.Routing;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Owns explicit CAS contention without replacing serialized durable state or its ETag checks.</summary>
internal sealed class ControlledCatalogContentionStore(IRouteCatalogStateStore inner) : IRouteCatalogStateStore
{
    private int _reads;
    private int _attempts;
    private int _allowExchange;
    /// <summary>Gets the actual underlying state coordinates.</summary>
    public RouteCatalogStateCoordinates? Coordinates => inner.Coordinates;
    /// <summary>Gets or sets whether the independent contention owner has released its contention.</summary>
    internal bool AllowExchange { get => Volatile.Read(ref _allowExchange) != 0; set => Volatile.Write(ref _allowExchange, value ? 1 : 0); }
    /// <summary>Gets or sets an independently controlled state change at a refused CAS.</summary>
    internal Func<int, CancellationToken, Task>? Conflict { get; set; }
    /// <summary>Gets the count of fresh state reads.</summary>
    internal int Reads => Volatile.Read(ref _reads);
    /// <summary>Gets the count of actual attempted state exchanges.</summary>
    internal int Attempts => Volatile.Read(ref _attempts);
    /// <inheritdoc/>
    public Task<(RouteCatalogActivationState State, string ETag)> ReadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Interlocked.Increment(ref _reads);
        return inner.ReadAsync(cancellationToken);
    }
    /// <inheritdoc/>
    public async Task<bool> CompareExchangeAsync(RouteCatalogActivationState state, string etag, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        int attempt = Interlocked.Increment(ref _attempts);
        if (!AllowExchange)
        {
            if (Conflict is { } conflict) await conflict(attempt, cancellationToken);
            return false;
        }
        return await inner.CompareExchangeAsync(state, etag, cancellationToken);
    }
}
