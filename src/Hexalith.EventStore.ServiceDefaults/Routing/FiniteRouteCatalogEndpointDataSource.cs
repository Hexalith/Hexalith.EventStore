using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Primitives;

namespace Hexalith.EventStore.ServiceDefaults.Routing;

/// <summary>Shares actual endpoint references between routing and the independently enrolled finite receiver.</summary>
internal sealed class FiniteRouteCatalogEndpointDataSource : EndpointDataSource
{
    private readonly EndpointDataSource _source;
    private readonly object _gate = new();
    private IReadOnlyList<Endpoint>? _endpoints;
    private IChangeToken? _version;

    /// <summary>Wraps a host-owned source without taking over its disposal.</summary>
    internal FiniteRouteCatalogEndpointDataSource(EndpointDataSource source) => _source = source;

    /// <summary>Retains one materialization until the original source signals a change.</summary>
    public override IReadOnlyList<Endpoint> Endpoints
    {
        get
        {
            lock (_gate)
            {
                if (_endpoints is not null && !_version!.HasChanged) return _endpoints;
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    IChangeToken version = _source.GetChangeToken();
                    IReadOnlyList<Endpoint> endpoints = _source.Endpoints;
                    if (version.HasChanged) continue;
                    _version = version;
                    _endpoints = endpoints;
                    return endpoints;
                }
                throw new RouteCatalogUnavailableException("The actual endpoint source changed during bounded materialization.",
                    RouteCatalogRefusalReason.Inventory);
            }
        }
    }

    /// <summary>Preserves the original source's dynamic invalidation contract.</summary>
    public override IChangeToken GetChangeToken() => _source.GetChangeToken();

    /// <summary>Preserves grouped route materialization outside the root routing cache.</summary>
    public override IReadOnlyList<Endpoint> GetGroupedEndpoints(RouteGroupContext context) => _source.GetGroupedEndpoints(context);
}
