using Hexalith.EventStore.Controllers;
using Hexalith.EventStore.Server.Routing;
using Hexalith.EventStore.ServiceDefaults.Routing;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

public class DevelopmentGatewayAdminMetadataTests
{
    [Fact]
    public async Task ActualGatewayStorageAndProjectionActionsExposeExactTechnicalTransport()
    {
        await using var fixture = new TechnicalHttpCatalogFixture();
        await fixture.StartAsync(TestContext.Current.CancellationToken, fullInventory: true, inventoryAssembly: "Hexalith.EventStore.Gateway");
        var surface = fixture.App.Services.GetRequiredService<TechnicalHttpRouteCatalogReceiver>().Observe();
        var endpoints = ((IEndpointRouteBuilder)fixture.App).DataSources.SelectMany(source => source.Endpoints).OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.Metadata.GetMetadata<ControllerActionDescriptor>()?.ControllerTypeInfo.AsType() is { } type
                && (type == typeof(AdminStorageCommandController) || type == typeof(AdminProjectionRebuildController))).ToArray();
        endpoints.ShouldNotBeEmpty();
        endpoints.ShouldContain(endpoint => endpoint.RoutePattern.Parameters.Count > 0);
        foreach (var endpoint in endpoints)
        {
            var transport = DevelopmentTechnicalHttpTransport.Observe(endpoint).ShouldNotBeNull(endpoint.RoutePattern.RawText);
            transport.CodeDigest.Length.ShouldBe(64);
            RunScopedRouteCatalogGateway.Operation(endpoint).ShouldBeNull();
            var descriptor = surface.TechnicalHttp!.Single(action => action.Pattern.Trim('/') == endpoint.RoutePattern.RawText
                && action.Methods.SequenceEqual(endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()!.HttpMethods));
            descriptor.ActionIdentity.ShouldStartWith("Hexalith.EventStore.Gateway,");
            descriptor.RequestDigest.Length.ShouldBe(64); descriptor.ResponseDigest.Length.ShouldBe(64);
            descriptor.PolicyDigest.Length.ShouldBe(64); descriptor.FilterDigest.Length.ShouldBe(64);
        }
        var put = endpoints.Single(endpoint => endpoint.RoutePattern.RawText == "api/v1/admin/storage/snapshot-policy"
            && endpoint.Metadata.GetMetadata<Microsoft.AspNetCore.Routing.IHttpMethodMetadata>()!.HttpMethods.Contains("PUT"));
        DevelopmentTechnicalHttpTransport.Observe(put)!.Verbs.ShouldBe("PUT");
    }
}
