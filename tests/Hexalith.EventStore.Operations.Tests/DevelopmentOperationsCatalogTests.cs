using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace Hexalith.EventStore.Operations.Tests;

/// <summary>Checks actual executable operator delegates, retaining the existing human/channel/role trust boundaries.</summary>
public sealed class DevelopmentOperationsCatalogTests
{
    [Fact]
    public void ExactlyFiveExistingOperatorEndpointsHaveClosedCodeWitnesses()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        { builder.UseEnvironment("Development"); OperationsTestCredentials.Configure(builder); });
        using var client = factory.CreateClient();
        var actual = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.RoutePattern.RawText?.StartsWith("/internal/dead-letters", StringComparison.Ordinal) == true).ToArray();
        actual.Length.ShouldBe(5);
        foreach (var endpoint in actual)
        {
            var owner = DevelopmentTechnicalHttpTransport.Observe(endpoint).ShouldNotBeNull();
            owner.Owner.ShouldBe("Hexalith.EventStore.Operations.Endpoints.DeadLetterOperationsEndpointExtensions");
            owner.CodeDigest.Length.ShouldBe(64);
            owner.Verbs.ShouldBe(endpoint.RoutePattern.RawText is "/internal/dead-letters" or "/internal/dead-letters/count" ? "GET" : "POST");
            var foreign = new RouteEndpoint(static _ => Task.CompletedTask, endpoint.RoutePattern, endpoint.Order, endpoint.Metadata, endpoint.DisplayName);
            DevelopmentTechnicalHttpTransport.Observe(foreign).ShouldBeNull();
            var renamed = new RouteEndpoint(endpoint.RequestDelegate!, RoutePatternFactory.Parse("/internal/dead-letters/foreign"),
                endpoint.Order, endpoint.Metadata, endpoint.DisplayName);
            DevelopmentTechnicalHttpTransport.Observe(renamed).ShouldBeNull();
            var unprotected = new RouteEndpoint(endpoint.RequestDelegate!, endpoint.RoutePattern, endpoint.Order,
                new EndpointMetadataCollection(endpoint.Metadata.Where(item => item is not IAuthorizeData)), endpoint.DisplayName);
            DevelopmentTechnicalHttpTransport.Observe(unprotected).ShouldBeNull();
        }
    }
}
