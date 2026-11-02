using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.AspNetCore.Http;

namespace Hexalith.EventStore.DomainService;

/// <summary>Owns receiver authority and an optional SDK-built terminal refusal response.</summary>
internal sealed record DomainRouteCatalogDecision(RouteCatalogDispatchLease? Lease, IResult? TerminalRefusal = null) : IAsyncDisposable
{
    /// <inheritdoc/>
    public ValueTask DisposeAsync() => Lease?.DisposeAsync() ?? ValueTask.CompletedTask;
}
