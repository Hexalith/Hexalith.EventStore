using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.ServiceDefaults.Authentication;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hexalith.EventStore.ServiceDefaults.Tests.GeneratedRest;

/// <summary>Exercises the exact typed metadata and SDK validation callback emitted on generated actions.</summary>
[ApiController]
[Authorize(Policy = EventStoreHttpHostSecurityExtensions.HumanPolicy)]
public sealed class GeneratedRestProbeController(GeneratedRestProbeState state) : ControllerBase
{
    /// <summary>Reads one resolved tenant under the real generated-action catalog callback.</summary>
    [HttpGet("/api/probe/{tenantId}")]
    [RouteCatalogEndpoint(typeof(GeneratedRestProbeQuery), "query")]
    public IActionResult Get(string tenantId)
    {
        if (!RouteCatalogGeneratedAction.ValidateTenant(HttpContext.Items, typeof(GeneratedRestProbeQuery), "query", tenantId)) return StatusCode(503);
        state.Record(tenantId);
        return Ok("observed");
    }

    /// <summary>An untyped neighboring GET must never inherit a Dapr grant.</summary>
    [HttpGet("/api/untyped/{tenantId}")]
    public IActionResult Untyped(string tenantId) { state.Record(tenantId); return Ok("untyped"); }

    /// <summary>Non-query typed actions must never inherit the generated GET fence.</summary>
    [HttpPost("/api/command/{tenantId}")]
    [RouteCatalogEndpoint(typeof(GeneratedRestProbeQuery), "command")]
    public IActionResult Command(string tenantId) { state.Record(tenantId); return Ok("command"); }
}
