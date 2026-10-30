using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Primitives;

namespace Hexalith.EventStore.ServiceDefaults.Tests;

/// <summary>Publishes real routing invalidation while retaining the test's source ownership.</summary>
internal sealed class FiniteReceiverMutableEndpointSource(Endpoint endpoint) : EndpointDataSource, IDisposable
{
    private Endpoint[] _endpoints = [endpoint];
    private CancellationTokenSource _changes = new();

    /// <inheritdoc/>
    public override IReadOnlyList<Endpoint> Endpoints
    {
        get
        {
            Reads++;
            Endpoint[] current = _endpoints;
            if (InvalidateDuringRead) Replace(current.Single());
            return current;
        }
    }

    /// <summary>Gets the number of actual source materializations.</summary>
    internal int Reads { get; private set; }

    /// <summary>Invalidates every materialization to exercise bounded refusal during capture.</summary>
    internal bool InvalidateDuringRead { get; init; }

    /// <inheritdoc/>
    public override IChangeToken GetChangeToken() => new CancellationChangeToken(_changes.Token);

    /// <summary>Replaces the actual routed endpoint and invalidates every consumer cache.</summary>
    internal void Replace(Endpoint endpoint)
    {
        _endpoints = [endpoint];
        CancellationTokenSource previous = _changes;
        _changes = new CancellationTokenSource();
        previous.Cancel();
        previous.Dispose();
    }

    /// <summary>Releases the source's final change notification owner.</summary>
    public void Dispose() => _changes.Dispose();
}
