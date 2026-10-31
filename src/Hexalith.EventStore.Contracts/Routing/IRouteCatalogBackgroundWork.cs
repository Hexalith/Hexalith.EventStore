namespace Hexalith.EventStore.Contracts.Routing;

/// <summary>Owns one detached workflow through known completion, retaining actual ambiguous RPC effects.</summary>
public interface IRouteCatalogBackgroundWork : IAsyncDisposable
{
    /// <summary>Executes once in its own catalog scope and releases only on known completion.</summary>
    Task RunAsync(Func<Task> execute);
}
