using System.Reflection;

using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;

namespace Hexalith.EventStore.Sample.Tests;

/// <summary>Runs isolated response assertions through the same real filter and leased catalog as hosted actions.</summary>
internal static class SampleApiGeneratedFilterFixture
{
    internal static async Task<IActionResult> ExecuteAsync(ControllerBase controller, MethodInfo method, Func<Task<IActionResult>> invoke)
    {
        var descriptor = new ControllerActionDescriptor
        {
            MethodInfo = method, ControllerTypeInfo = controller.GetType().GetTypeInfo(),
            EndpointMetadata = method.GetCustomAttributes().Cast<object>().ToList(),
        };
        var context = new ActionContext(controller.HttpContext, new RouteData(), descriptor, controller.ModelState);
        var executing = new ActionExecutingContext(context, [], new Dictionary<string, object?>(), controller);
        ActionExecutedContext? executed = null;
        await new GeneratedRouteCatalogFilter(new SampleApiCommittedCatalog()).OnActionExecutionAsync(executing, async () =>
        {
            executed = new ActionExecutedContext(context, [], controller) { Result = await invoke().ConfigureAwait(false) };
            return executed;
        }).ConfigureAwait(false);
        return executing.Result ?? executed?.Result ?? throw new InvalidOperationException("The actual generated filter produced no action result.");
    }
}
