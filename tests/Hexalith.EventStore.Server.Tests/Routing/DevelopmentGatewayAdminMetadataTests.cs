using Hexalith.EventStore.Controllers;
using Hexalith.EventStore.Server.Routing;
using Hexalith.EventStore.ServiceDefaults.Routing;
using Microsoft.AspNetCore.Builder;
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
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddControllers().AddApplicationPart(typeof(AdminStorageCommandController).Assembly);
        await using var app = builder.Build(); app.MapControllers();
        var endpoints = ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints).OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.Metadata.GetMetadata<ControllerActionDescriptor>()?.ControllerTypeInfo.AsType() is { } type
                && (type == typeof(AdminStorageCommandController) || type == typeof(AdminProjectionRebuildController))).ToArray();
        endpoints.ShouldNotBeEmpty();
        endpoints.ShouldContain(endpoint => endpoint.RoutePattern.Parameters.Count > 0);
        foreach (var endpoint in endpoints)
        {
            var transport = DevelopmentTechnicalHttpTransport.Observe(endpoint).ShouldNotBeNull(endpoint.RoutePattern.RawText);
            transport.CodeDigest.Length.ShouldBe(64);
            RunScopedRouteCatalogGateway.Operation(endpoint).ShouldBe(DevelopmentTechnicalHttpTransport.Operation(endpoint));
        }
        var put = endpoints.Single(endpoint => endpoint.RoutePattern.RawText == "api/v1/admin/storage/snapshot-policy"
            && endpoint.Metadata.GetMetadata<Microsoft.AspNetCore.Routing.IHttpMethodMetadata>()!.HttpMethods.Contains("PUT"));
        DevelopmentTechnicalHttpTransport.Observe(put)!.Verbs.ShouldBe("PUT");
    }
}
