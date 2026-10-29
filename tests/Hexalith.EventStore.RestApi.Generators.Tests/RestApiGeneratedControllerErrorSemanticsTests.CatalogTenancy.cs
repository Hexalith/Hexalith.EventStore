using System.Security.Claims;

using Hexalith.EventStore.Contracts.Routing;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Hexalith.EventStore.RestApi.Generators.Tests;

public sealed partial class RestApiGeneratedControllerErrorSemanticsTests
{
    [Theory]
    [InlineData("Route", false)]
    [InlineData("Claims", false)]
    [InlineData("System", false)]
    [InlineData("Route", true)]
    [InlineData("Claims", true)]
    [InlineData("System", true)]
    public async Task GeneratedActionsSupplyTheResolvedPartitionTenantToExactCatalogValidation(string mode, bool conflicting)
    {
        RestApiGeneratedController controller = CreateController(tenantMode: mode);
        string selectedTenant = mode == "System" ? "system" : mode == "Claims" ? "claimed-tenant" : "route-tenant";
        string routeIdentifier = mode == "Route" ? selectedTenant : "aggregate-route-identifier";
        controller.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity([new("eventstore:tenant", "claimed-tenant")], "test"));
        var observed = new List<(string Kind, string Tenant)>();
        controller.HttpContext.Items[RouteCatalogGeneratedAction.ValidatorKey] = (Func<Type, string, string, bool>)((_, kind, tenant) =>
        {
            observed.Add((kind, tenant));
            return tenant == (conflicting ? "unselected-tenant" : selectedTenant);
        });
        object body = Activator.CreateInstance(controller.Assembly.GetType("Smoke.IncrementCounter")!, "counter-1", 2)!;
        var command = (Task<IActionResult>)controller.Controller.GetType().GetMethod("IncrementCounterCommandAsync")!
            .Invoke(controller.Controller, [routeIdentifier, "counter-1", body, TestContext.Current.CancellationToken])!;
        IActionResult commandResult = await command;
        if (conflicting) commandResult.ShouldBeOfType<StatusCodeResult>().StatusCode.ShouldBe(503);
        else commandResult.ShouldBeOfType<AcceptedResult>();
        controller.Gateway.CommandCallCount.ShouldBe(conflicting ? 0 : 1);
        int queries = 0;
        controller.Gateway.QueryHandler = (request, _, _) =>
        {
            queries++;
            request.Tenant.ShouldBe(selectedTenant);
            request.AggregateId.ShouldBe("counter-1");
            return Task.FromResult(new Hexalith.EventStore.Client.Gateway.EventStoreQueryResult("correlation", null, false, null));
        };
        var query = (Task<IActionResult>)controller.Controller.GetType().GetMethod("GetCounterStatusQueryQueryAsync")!
            .Invoke(controller.Controller, [routeIdentifier, "counter-1", null, TestContext.Current.CancellationToken])!;
        IActionResult queryResult = await query;
        if (conflicting) queryResult.ShouldBeOfType<StatusCodeResult>().StatusCode.ShouldBe(503);
        queries.ShouldBe(conflicting ? 0 : 1);
        observed.ShouldBe([("command", selectedTenant), ("query", selectedTenant)]);
    }

    [Theory]
    [InlineData("Route")]
    [InlineData("Claims")]
    [InlineData("System")]
    public async Task GeneratedBodyAggregateMismatchRejectsBeforeCatalogAndGateway(string mode)
    {
        RestApiGeneratedController controller = CreateController(tenantMode: mode);
        controller.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity([new("eventstore:tenant", "claimed-tenant")], "test"));
        int inspections = 0;
        controller.HttpContext.Items[RouteCatalogGeneratedAction.ValidatorKey] = (Func<Type, string, string, bool>)((_, _, _) => { inspections++; return true; });
        object body = Activator.CreateInstance(controller.Assembly.GetType("Smoke.IncrementCounter")!, "body-id", 2)!;
        var command = (Task<IActionResult>)controller.Controller.GetType().GetMethod("IncrementCounterCommandAsync")!
            .Invoke(controller.Controller, ["route-tenant", "different-route-id", body, TestContext.Current.CancellationToken])!;
        (await command).ShouldBeOfType<ObjectResult>().StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        inspections.ShouldBe(0);
        controller.Gateway.CommandCallCount.ShouldBe(0);
    }
}
