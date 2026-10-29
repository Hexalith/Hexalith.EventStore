using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.Sample.Counter.Commands;

using Microsoft.AspNetCore.Mvc;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Exercises result processing through the actual MVC filter pipeline.</summary>
[ApiController]
[Route("catalog-generated-probe")]
public sealed class CatalogGeneratedMvcProbeController : ControllerBase
{
    /// <summary>Returns an apparently successful action without validating its partition.</summary>
    [HttpGet("missing")]
    [RouteCatalogEndpoint(typeof(IncrementCounter), "command")]
    public IActionResult Missing() => Ok("missing validation");

    /// <summary>Uses the same resolved-tenant callback emitted by generated actions.</summary>
    [HttpGet("valid")]
    [RouteCatalogEndpoint(typeof(IncrementCounter), "command")]
    public IActionResult Valid()
    {
        if (!RouteCatalogGeneratedAction.ValidateTenant(HttpContext.Items, typeof(IncrementCounter), "command", "tenant-a")) return StatusCode(503);
        (HttpContext.RequestServices.GetService(typeof(CatalogGeneratedEffectCounter)) as CatalogGeneratedEffectCounter)?.Record();
        return Ok("validated");
    }

    /// <summary>Models an early generated route/body validation refusal.</summary>
    [HttpGet("early-bad-request")]
    [RouteCatalogEndpoint(typeof(IncrementCounter), "command")]
    public IActionResult EarlyBadRequest() => BadRequest("bad aggregate identity");

    /// <summary>Models an existing authorization denial before dispatch.</summary>
    [HttpGet("early-forbidden")]
    [RouteCatalogEndpoint(typeof(IncrementCounter), "command")]
    public IActionResult EarlyForbidden() => StatusCode(403, "authority denied");

    /// <summary>Preserves a bounded failure produced before downstream execution.</summary>
    [HttpGet("early-error")]
    [RouteCatalogEndpoint(typeof(IncrementCounter), "command")]
    public IActionResult EarlyError() => StatusCode(500, "upstream failure");
}
