using Hexalith.EventStore.ServiceDefaults.Routing;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Delays only a real peer's first persisted read after its independent observation was published.</summary>
internal sealed class BootstrapReadGateStore(IRouteCatalogStateStore inner) : IRouteCatalogStateStore
{
    private int _reads;
    internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal TaskCompletionSource Continue { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public RouteCatalogStateCoordinates? Coordinates => inner.Coordinates;

    public async Task<(RouteCatalogActivationState State, string ETag)> ReadAsync(CancellationToken token)
    {
        if (Interlocked.Increment(ref _reads) == 1)
        {
            Entered.TrySetResult();
            await Continue.Task.WaitAsync(token);
        }
        return await inner.ReadAsync(token);
    }

    public Task<bool> CompareExchangeAsync(RouteCatalogActivationState state, string etag, CancellationToken token)
        => inner.CompareExchangeAsync(state, etag, token);
}
