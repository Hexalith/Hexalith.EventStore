using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Primitives;

namespace Hexalith.EventStore.DomainService.Tests;

/// <summary>Publishes an actual routing change to test replacement after startup inventory validation.</summary>
/// <param name="endpoint">The initial classified endpoint.</param>
internal sealed class HttpHostSecurityMutableEndpointSource(Endpoint endpoint) : EndpointDataSource
{
    private Endpoint[] _endpoints = [endpoint];
    private CancellationTokenSource _changes = new();

    /// <inheritdoc />
    public override IReadOnlyList<Endpoint> Endpoints => _endpoints;

    /// <inheritdoc />
    public override IChangeToken GetChangeToken() => new CancellationChangeToken(_changes.Token);

    /// <summary>Publishes a replacement and invalidates the routing matcher.</summary>
    internal void Replace(Endpoint replacement)
    {
        _endpoints = [replacement];
        CancellationTokenSource previous = _changes;
        _changes = new CancellationTokenSource();
        previous.Cancel();
        previous.Dispose();
    }
}
