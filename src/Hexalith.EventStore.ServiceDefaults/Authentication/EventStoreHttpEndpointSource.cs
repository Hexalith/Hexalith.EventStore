using Microsoft.AspNetCore.Routing;

namespace Hexalith.EventStore.ServiceDefaults.Authentication;

/// <summary>Retains live endpoint data sources until startup mappings are complete.</summary>
internal sealed class EventStoreHttpEndpointSource
{
    /// <summary>Gets or sets the host endpoint builder.</summary>
    internal IEndpointRouteBuilder? Endpoints { get; set; }
}
