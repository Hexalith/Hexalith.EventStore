using Microsoft.AspNetCore.Routing;

namespace Hexalith.EventStore.DomainService;

/// <summary>
/// Holds the route builder of the domain-service host so the startup inventory sees every mapped endpoint,
/// including host overrides mapped before or after the SDK routes.
/// </summary>
public sealed class EventStoreDomainServiceEndpointSource
{
    private IEndpointRouteBuilder? _endpoints;

    /// <summary>Gets the captured route builder, or <see langword="null"/> when no SDK route was mapped.</summary>
    public IEndpointRouteBuilder? Endpoints => _endpoints;

    /// <summary>Captures the route builder that maps the SDK routes.</summary>
    /// <param name="endpoints">The route builder.</param>
    public void Capture(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        _ = Interlocked.CompareExchange(ref _endpoints, endpoints, null);
    }
}
