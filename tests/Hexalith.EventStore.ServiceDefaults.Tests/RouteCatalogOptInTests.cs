using Hexalith.EventStore.Contracts.Authorization;
using Hexalith.EventStore.ServiceDefaults;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.ServiceDefaults.Tests;

public sealed class RouteCatalogOptInTests
{
    [Fact]
    public void AddServiceDefaultsAloneDoesNotRegisterOrMapTheRouteCatalog()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        _ = builder.AddServiceDefaults();
        using WebApplication app = builder.Build();
        _ = app.MapDefaultEndpoints();

        app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<HealthCheckServiceOptions>>().Value.Registrations
            .ShouldNotContain(registration => registration.Name == "route-catalog");
        RouteEndpoints(app).ShouldNotContain(endpoint => endpoint.RoutePattern.RawText!.Contains("internal/routing/catalog"));
    }

    [Fact]
    public void OptedInHostMapsEveryControlRouteBehindItsExactWorkloadPolicy()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        _ = builder.AddServiceDefaults();
        _ = builder.Services.AddEventStoreRouteCatalog(builder.Configuration);
        _ = builder.Services.AddSingleton(Substitute.For<IRouteCatalogAuthority>());
        using WebApplication app = builder.Build();
        _ = app.MapDefaultEndpoints();

        string read = EventStoreWorkloadAuthenticationDefaults.OperationPolicy(RouteCatalogEndpointExtensions.WorkloadScheme, EventStoreOperationIdentifiers.RouteCatalogRead);
        string activate = EventStoreWorkloadAuthenticationDefaults.OperationPolicy(RouteCatalogEndpointExtensions.WorkloadScheme, EventStoreOperationIdentifiers.RouteCatalogActivate);
        Dictionary<string, string> expected = new(StringComparer.Ordinal)
        {
            ["/" + RouteCatalogEndpointExtensions.ReadbackMethod] = read,
            ["/internal/routing/catalog/prepare"] = activate,
            ["/internal/routing/catalog/ready"] = activate,
            ["/internal/routing/catalog/commit"] = activate,
            ["/internal/routing/catalog/rollback"] = activate,
            ["/internal/routing/catalog/reconcile"] = activate,
        };

        RouteEndpoint[] mapped = [.. RouteEndpoints(app).Where(endpoint => endpoint.RoutePattern.RawText!.Contains("internal/routing/catalog"))];
        mapped.Select(endpoint => endpoint.RoutePattern.RawText!).Order(StringComparer.Ordinal)
            .ShouldBe(expected.Keys.Order(StringComparer.Ordinal));
        foreach (RouteEndpoint endpoint in mapped)
        {
            endpoint.Metadata.GetMetadata<IAllowAnonymous>().ShouldBeNull(endpoint.RoutePattern.RawText);
            endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Select(data => data.Policy)
                .ShouldBe([expected[endpoint.RoutePattern.RawText!]], endpoint.RoutePattern.RawText);
        }
    }

    private static IEnumerable<RouteEndpoint> RouteEndpoints(WebApplication app)
        => ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints).OfType<RouteEndpoint>();
}
