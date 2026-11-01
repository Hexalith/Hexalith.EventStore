using Hexalith.EventStore.Contracts.Routing;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Request-owned probe that observes construction, isolation and disposal without domain effects.</summary>
internal sealed class TerminalRefusalScopedService : IDisposable
{
    internal Guid Id { get; } = Guid.NewGuid();
    internal bool ConstructedInsideRemoteScope { get; } = RouteCatalogRemoteExecution.Current is not null;
    internal TaskCompletionSource Disposed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <inheritdoc/>
    public void Dispose() => Disposed.TrySetResult();
}
