using System.Security.Claims;

using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Queries;

using Microsoft.AspNetCore.Mvc;

namespace Hexalith.EventStore.RestApi.Generators.Tests;

public sealed partial class RestApiGeneratedControllerErrorSemanticsTests
{
    [Theory]
    [InlineData("RestQueryBindingSource.Constant, \"aggregate name\", RestQueryBindingSource.Constant, \"entity name\"", "aggregate name", "entity name")]
    [InlineData("RestQueryBindingSource.Constant, \"index\"", "index", null)]
    [InlineData("RestQueryBindingSource.Constant, \"index\", RestQueryBindingSource.None, null", "index", null)]
    public async Task QueryAction_ExplicitBindings_ForwardExactLiteralAndNullIdentifiers(string binding, string aggregateId, string? entityId)
    {
        RestApiGeneratedController controller = CreateController(queryBinding: binding);
        SubmitQueryRequest? captured = null;
        controller.Gateway.QueryHandler = (request, _, _) =>
        {
            captured = request;
            return Task.FromResult(new EventStoreQueryResult("correlation", null, false, null));
        };

        IActionResult result = await InvokeQueryAsync(controller).ConfigureAwait(true);

        result.ShouldBeOfType<OkObjectResult>();
        captured.ShouldNotBeNull().AggregateId.ShouldBe(aggregateId);
        captured.EntityId.ShouldBe(entityId);
    }

    [Theory]
    [InlineData("Route", "Inherit", "tenant-a")]
    [InlineData("Claims", "Inherit", "claimed-tenant")]
    [InlineData("Route", "Claims", "claimed-tenant")]
    [InlineData("Claims", "Route", "tenant-a")]
    [InlineData("Inherit", "Inherit", "claimed-tenant")]
    public async Task QueryAction_TenantInheritanceAndOverride_ForwardResolvedTenant(string assemblyMode, string routeMode, string expectedTenant)
    {
        RestApiGeneratedController controller = CreateController(tenantMode: assemblyMode, routeTenantMode: routeMode);
        controller.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new("eventstore:tenant", "tenant-a"), new("eventstore:tenant", "claimed-tenant")], "test"));
        if (expectedTenant == "claimed-tenant")
        {
            controller.HttpContext.Request.Headers["X-Hexalith-Tenant"] = expectedTenant;
        }

        SubmitQueryRequest? captured = null;
        controller.Gateway.QueryHandler = (request, _, _) =>
        {
            captured = request;
            return Task.FromResult(new EventStoreQueryResult("correlation", null, false, null));
        };

        IActionResult result = await InvokeQueryAsync(controller).ConfigureAwait(true);

        result.ShouldBeOfType<OkObjectResult>();
        captured.ShouldNotBeNull().Tenant.ShouldBe(expectedTenant);
    }
}
