using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.Sample.Counter.Commands;
using Hexalith.EventStore.ServiceDefaults.Authentication;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.EventStore.Server.Tests.Routing;

[ApiController]
[Route("catalog-v3-generated-probe")]
[Authorize(Policy = EventStoreHttpHostSecurityExtensions.HumanPolicy)]
public sealed class CatalogV3GeneratedProbeController : ControllerBase
{
    [HttpGet]
    [RouteCatalogEndpoint(typeof(IncrementCounter), "command")]
    public IActionResult Invoke()
    {
        if (!RouteCatalogGeneratedAction.ValidateTenant(HttpContext.Items, typeof(IncrementCounter), "command", "tenant-a")) return StatusCode(503);
        HttpContext.RequestServices.GetRequiredService<CatalogGeneratedEffectCounter>().Record();
        return Ok("validated");
    }
}
