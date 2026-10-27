using System.Security.Cryptography;
using Hexalith.EventStore.Client.Discovery;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Sample.Counter;
using Hexalith.EventStore.Sample.Counter.Events;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shouldly;

namespace Hexalith.EventStore.Sample.Tests;

public class SampleCatalogHostTests
{
    [Fact]
    public async Task RealHostProjectionInventoryDispatchesCounterAndOnlyDeclaredOutputs()
    {
        await using WebApplicationFactory<CounterAggregate> factory = Factory();
        using HttpClient client = factory.CreateClient();
        IDomainProjectionHandler[] handlers = factory.Services.GetServices<IDomainProjectionHandler>().ToArray();
        AssertLegacyInventory(handlers, [SampleCatalog.Projection]);
        factory.Services.GetRequiredService<DiscoveryResult>().Projections.ShouldBeEmpty();
        using IServiceScope scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetServices<IAsyncDomainProjectionHandler>().ShouldBeEmpty();

        var request = new ProjectionRequest("tenant-a", "counter", "counter-1",
            [new ProjectionEventDto(typeof(CounterIncremented).FullName!, "{}"u8.ToArray(), "json", 1, DateTimeOffset.UtcNow, "corr-1", "event-1", "actor-1")]);
        ProjectionResponse response = DomainProjectionDispatcher.Project(scope.ServiceProvider, request, TestContext.Current.CancellationToken).ShouldNotBeNull();
        response.ProjectionType.ShouldBe(SampleCatalog.Projection.ProjectionType);
        SampleCatalog.Projection.Execution!.ProjectionOutputs.ShouldBe([response.ProjectionType]);
        response.State.GetProperty("count").GetInt32().ShouldBe(1);
        DomainProjectionDispatcher.Project(scope.ServiceProvider, request with { Domain = "greeting" }, TestContext.Current.CancellationToken).ShouldBeNull();

        Should.Throw<ShouldAssertException>(() => AssertLegacyInventory([], [SampleCatalog.Projection]));
        Should.Throw<ShouldAssertException>(() => AssertLegacyInventory([.. handlers, handlers[0]], [SampleCatalog.Projection]));
        Should.Throw<ShouldAssertException>(() => AssertLegacyInventory(handlers, [SampleCatalog.Projection with { Domain = "wrong-domain" }]));
        Should.Throw<ShouldAssertException>(() => AssertLegacyInventory(handlers, []));
    }

    [Fact]
    public async Task RemovingActualHostProjectionRegistrationFailsInventoryAndDispatch()
    {
        await using WebApplicationFactory<CounterAggregate> original = Factory();
        await using WebApplicationFactory<CounterAggregate> factory = original.WithWebHostBuilder(builder => builder.ConfigureServices(services => services.RemoveAll<IDomainProjectionHandler>()));
        using HttpClient client = factory.CreateClient();
        Should.Throw<ShouldAssertException>(() => AssertLegacyInventory(factory.Services.GetServices<IDomainProjectionHandler>().ToArray(), [SampleCatalog.Projection]));
        DomainProjectionDispatcher.Project(factory.Services, new("tenant-a", "counter", "counter-1", []), TestContext.Current.CancellationToken).ShouldBeNull();
    }

    private static void AssertLegacyInventory(IReadOnlyList<IDomainProjectionHandler> handlers, IReadOnlyList<RouteContractDeclaration> declarations)
    {
        handlers.Select(handler => (FullName: handler.GetType().FullName!, handler.Domain)).OrderBy(value => value.FullName)
            .ShouldBe(declarations.Select(route => (route.Execution!.HandlerType, route.Domain)).OrderBy(value => value.HandlerType));
    }

    private static WebApplicationFactory<CounterAggregate> Factory() => new WebApplicationFactory<CounterAggregate>().WithWebHostBuilder(builder => builder
        .UseEnvironment("Development")
        .UseSetting("EventStore:DomainService:AppId", "sample")
        .UseSetting("EventStore:DataProtection:PersistToStateStore", "false")
        .UseSetting(DaprAppChannelToken.ConfigurationKey, "sample-catalog-channel")
        .UseSetting("Authentication:JwtBearer:Issuer", "catalog-test")
        .UseSetting("Authentication:JwtBearer:Audience", "sample")
        .UseSetting("Authentication:JwtBearer:SigningKey", Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)))
        .UseSetting("Authentication:JwtBearer:RequireHttpsMetadata", "false"));
}
