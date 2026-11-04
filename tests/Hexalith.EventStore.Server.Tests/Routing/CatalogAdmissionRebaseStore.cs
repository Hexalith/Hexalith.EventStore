using Hexalith.EventStore.ServiceDefaults.Routing;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Injects deterministic races around an actual serialized, ETag-checked authority store.</summary>
internal sealed class CatalogAdmissionRebaseStore(IRouteCatalogStateStore inner) : IRouteCatalogStateStore
{
    /// <inheritdoc/>
    public RouteCatalogStateCoordinates? Coordinates => inner.Coordinates;
    /// <summary>Gets the observed read count.</summary>
    internal int Reads { get; private set; }
    /// <summary>Gets the attempted CAS count.</summary>
    internal int Exchanges { get; private set; }
    /// <summary>Gets the exact read concurrency tokens.</summary>
    internal List<string> ReadETags { get; } = [];
    /// <summary>Gets the exact attempted concurrency tokens.</summary>
    internal List<string> ExchangeETags { get; } = [];
    /// <summary>Gets or sets an action before the selected fresh read.</summary>
    internal Func<int, CancellationToken, Task>? BeforeRead { get; set; }
    /// <summary>Gets or sets a deliberately aliased read result.</summary>
    internal Func<int, RouteCatalogActivationState, RouteCatalogActivationState>? TransformRead { get; set; }
    /// <summary>Gets or sets an action that can model contention or response loss at CAS.</summary>
    internal Func<int, RouteCatalogActivationState, string, CancellationToken, Task<bool?>>? Exchange { get; set; }
    /// <inheritdoc/>
    public async Task<(RouteCatalogActivationState State, string ETag)> ReadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        int read = ++Reads;
        if (BeforeRead is { } before) await before(read, cancellationToken);
        var result = await inner.ReadAsync(cancellationToken);
        ReadETags.Add(result.ETag);
        return (TransformRead?.Invoke(read, result.State) ?? result.State, result.ETag);
    }
    /// <inheritdoc/>
    public async Task<bool> CompareExchangeAsync(RouteCatalogActivationState state, string etag, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        int exchange = ++Exchanges;
        ExchangeETags.Add(etag);
        if (Exchange is { } change && await change(exchange, state, etag, cancellationToken) is { } result) return result;
        return await inner.CompareExchangeAsync(state, etag, cancellationToken);
    }
}
