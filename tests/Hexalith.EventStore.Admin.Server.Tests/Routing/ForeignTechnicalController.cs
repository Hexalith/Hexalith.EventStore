using Microsoft.AspNetCore.Mvc;
namespace Hexalith.EventStore.Admin.Server.Tests.Routing;

[Microsoft.AspNetCore.Authorization.Authorize(Policy = Hexalith.EventStore.ServiceDefaults.Authentication.EventStoreHttpHostSecurityExtensions.HumanPolicy)]
[NonController]
[ApiController]
[Route("foreign-mvc")]
public sealed class ForeignTechnicalController : ControllerBase
{
    [HttpGet]
    public string Get() => GetType().Name;
}
