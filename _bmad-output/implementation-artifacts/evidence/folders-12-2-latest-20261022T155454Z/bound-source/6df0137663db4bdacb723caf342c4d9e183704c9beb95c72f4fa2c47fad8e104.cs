using System.Reflection;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Hexalith.EventStore.RestApi.Generators.Tests;

internal sealed record RestApiGeneratedController(
    Assembly Assembly,
    FakeEventStoreGatewayClient Gateway,
    ControllerBase Controller)
{
    public HttpContext HttpContext => Controller.HttpContext;
}
