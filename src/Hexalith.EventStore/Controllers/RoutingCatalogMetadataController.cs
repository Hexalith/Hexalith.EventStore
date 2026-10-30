using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hexalith.EventStore.Controllers;

/// <summary>Authenticated discovery of executable public operation contracts from one held committed generation.</summary>
[ApiController]
[Route("api/v1/routing/catalog")]
[Authorize(Policy = EventStoreHttpHostSecurityExtensions.HumanPolicy)]
public sealed class RoutingCatalogMetadataController(IRouteCatalogAuthority authority) : ControllerBase
{
    /// <summary>Returns schemas and eligibility without keys, retained history or internal target coordinates.</summary>
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var lease = await authority.AcquireAsync(cancellationToken).ConfigureAwait(false);
        await using var release = lease.ConfigureAwait(false);
        var envelope = lease.Snapshot.Envelope;
        if (envelope.SchemaVersion != 3) return StatusCode(503);
        if (RouteCatalogSurfacePolicy.Surface(User) is null) return Forbid();
        Response.Headers.CacheControl = "no-store";
        return Ok(new
        {
            envelope.SchemaVersion, envelope.Generation, lease.Snapshot.RootDigest,
            operations = envelope.Operations!.Where(o => o.Ingress == "public").Select(operation => new
            {
                operation.RouteId, operation.ContractVersion, operation.Eligibility, operation.Digest,
                operation.RequestSchema, operation.ResultSchema,
                kind = envelope.Declarations.Single(d => d.RouteId == operation.RouteId).Kind,
                domain = envelope.Declarations.Single(d => d.RouteId == operation.RouteId).Domain,
                messageType = envelope.Declarations.Single(d => d.RouteId == operation.RouteId).MessageType,
            }).ToArray(),
        });
    }
}
