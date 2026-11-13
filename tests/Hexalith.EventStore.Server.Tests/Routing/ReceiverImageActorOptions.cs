using Dapr.Actors.Runtime;

using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Records each original native registration read without changing the real runtime options.</summary>
internal sealed class ReceiverImageActorOptions(IOptions<ActorRuntimeOptions> inner) : IOptions<ActorRuntimeOptions>
{
    private readonly List<RouteCatalogImageDigestScope?> _scopes = [];
    internal Action<int>? OnRead { get; set; }
    internal IReadOnlyList<RouteCatalogImageDigestScope?> Scopes => _scopes.ToArray();
    public ActorRuntimeOptions Value
    {
        get
        {
            _scopes.Add(RouteCatalogImageDigestScope.Current);
            OnRead?.Invoke(_scopes.Count);
            return inner.Value;
        }
    }
    internal void Reset() { _scopes.Clear(); OnRead = null; }
}
