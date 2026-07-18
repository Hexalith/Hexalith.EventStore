
using System.Reflection;

using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Server.Pipeline.Queries;
using Hexalith.EventStore.Server.Queries;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Queries;

public class SafeDenialQueryRoutingServiceCollectionExtensionsTests {
    private sealed class StubQueryRouter(QueryRouterResult result) : IQueryRouter {
        public Task<QueryRouterResult> RouteQueryAsync(SubmitQuery query, CancellationToken cancellationToken = default)
            => Task.FromResult(result);
    }

    private sealed class CustomSafeDenialQueryRoutePolicy : ISafeDenialQueryRoutePolicy {
        public bool IsOptedIn(string domain, string queryType) => true;
    }

    private static SubmitQuery CreateQuery(string domain = "orders", string queryType = "list-orders") =>
        new(
            Tenant: "test-tenant",
            Domain: domain,
            AggregateId: "order-index",
            QueryType: queryType,
            Payload: [],
            CorrelationId: "corr-1",
            UserId: "user-1");

    [Fact]
    public void AddEventStoreSafeDenialQueryRouting_NoPriorRegistration_ThrowsInvalidOperationException() {
        var services = new ServiceCollection();
        _ = services.AddLogging();

        Should.Throw<InvalidOperationException>(
            () => services.AddEventStoreSafeDenialQueryRouting([("orders", "list-orders")]));
    }

    [Fact]
    public void AddEventStoreSafeDenialQueryRouting_PriorRegistration_DecoratesWithSafeDenialQueryRouter() {
        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddScoped<IQueryRouter>(
            _ => new StubQueryRouter(new QueryRouterResult(Success: true, Payload: null, NotFound: false)));
        _ = services.AddEventStoreSafeDenialQueryRouting([("orders", "list-orders")]);

        using ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();

        IQueryRouter router = scope.ServiceProvider.GetRequiredService<IQueryRouter>();

        _ = router.ShouldBeOfType<SafeDenialQueryRouter>();
    }

    [Fact]
    public async Task AddEventStoreSafeDenialQueryRouting_DecoratesTheLastPriorRegistration() {
        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddScoped<IQueryRouter>(
            _ => new StubQueryRouter(new QueryRouterResult(Success: true, Payload: null, NotFound: false, ProjectionType: "first")));

        // Simulates an existing decorator chain (e.g. HandlerAwareQueryRouter) registered after
        // the base router — the safe-denial adapter must wrap this later registration, not the
        // first one, mirroring "last IQueryRouter registration wins" DI semantics.
        _ = services.AddScoped<IQueryRouter>(
            _ => new StubQueryRouter(new QueryRouterResult(
                Success: false,
                Payload: null,
                NotFound: false,
                ErrorMessage: QueryAdapterFailureReason.Forbidden)));
        _ = services.AddEventStoreSafeDenialQueryRouting([("orders", "list-orders")]);

        using ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();
        IQueryRouter router = scope.ServiceProvider.GetRequiredService<IQueryRouter>();

        QueryRouterResult result = await router.RouteQueryAsync(CreateQuery());

        // If the adapter had wrapped the first registration instead, this would be a successful
        // passthrough with ProjectionType "first" rather than the unified not-found shape.
        result.Success.ShouldBeFalse();
        result.NotFound.ShouldBeTrue();
        result.ErrorMessage.ShouldBeNull();
    }

    [Fact]
    public async Task AddEventStoreSafeDenialQueryRouting_ResolvedRouter_UnifiesForbiddenOnOptedInRoute() {
        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddScoped<IQueryRouter>(
            _ => new StubQueryRouter(new QueryRouterResult(
                Success: false,
                Payload: null,
                NotFound: false,
                ErrorMessage: QueryAdapterFailureReason.Forbidden)));
        _ = services.AddEventStoreSafeDenialQueryRouting([("orders", "list-orders")]);

        using ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();
        IQueryRouter router = scope.ServiceProvider.GetRequiredService<IQueryRouter>();

        QueryRouterResult result = await router.RouteQueryAsync(CreateQuery());

        result.Success.ShouldBeFalse();
        result.NotFound.ShouldBeTrue();
        result.ErrorMessage.ShouldBeNull();
    }

    [Fact]
    public async Task AddEventStoreSafeDenialQueryRouting_ResolvedRouter_NonOptedInRoute_PassesThroughForbiddenUnchanged() {
        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddScoped<IQueryRouter>(
            _ => new StubQueryRouter(new QueryRouterResult(
                Success: false,
                Payload: null,
                NotFound: false,
                ErrorMessage: QueryAdapterFailureReason.Forbidden)));

        // Registered routes cover a different domain/query type than the one under test.
        _ = services.AddEventStoreSafeDenialQueryRouting([("parties", "list-parties")]);

        using ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();
        IQueryRouter router = scope.ServiceProvider.GetRequiredService<IQueryRouter>();

        QueryRouterResult result = await router.RouteQueryAsync(CreateQuery());

        result.Success.ShouldBeFalse();
        result.NotFound.ShouldBeFalse();
        result.ErrorMessage.ShouldBe(QueryAdapterFailureReason.Forbidden);
    }

    // A second call to the extension must merge its route list into the first call's registry
    // instead of silently dropping it (TryAddSingleton alone would leave the first call's
    // narrower route set in place, so the second call's routes would never actually opt in).
    [Fact]
    public async Task AddEventStoreSafeDenialQueryRouting_CalledTwice_MergesRouteListsInsteadOfDroppingSecondCall() {
        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddScoped<IQueryRouter>(
            _ => new StubQueryRouter(new QueryRouterResult(
                Success: false,
                Payload: null,
                NotFound: false,
                ErrorMessage: QueryAdapterFailureReason.Forbidden)));
        _ = services.AddEventStoreSafeDenialQueryRouting([("orders", "list-orders")]);
        _ = services.AddEventStoreSafeDenialQueryRouting([("parties", "list-parties")]);

        using ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();
        IQueryRouter router = scope.ServiceProvider.GetRequiredService<IQueryRouter>();

        QueryRouterResult ordersResult = await router.RouteQueryAsync(CreateQuery(domain: "orders", queryType: "list-orders"));
        QueryRouterResult partiesResult = await router.RouteQueryAsync(CreateQuery(domain: "parties", queryType: "list-parties"));

        // Both calls' routes must be opted in -- if the second call had silently no-opped, the
        // "parties"/"list-parties" route would still see the unmodified Forbidden result.
        ordersResult.NotFound.ShouldBeTrue();
        partiesResult.NotFound.ShouldBeTrue();
    }

    // Repeated calls must end up with exactly one SafeDenialQueryRouter layer wrapping the true
    // original inner router, not N nested layers around each other: nesting is behaviorally
    // harmless (confirmed by the merge tests above passing either way) but architecturally
    // wasteful. Inspecting the resolved instance's own inner-router field (found by field *type*,
    // not name, since primary-constructor-captured fields are compiler-generated) proves it points
    // directly at the original StubQueryRouter rather than at another SafeDenialQueryRouter.
    [Fact]
    public void AddEventStoreSafeDenialQueryRouting_CalledMultipleTimes_FlattensToSingleDecoratorLayer() {
        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddScoped<IQueryRouter>(
            _ => new StubQueryRouter(new QueryRouterResult(Success: true, Payload: null, NotFound: false)));
        _ = services.AddEventStoreSafeDenialQueryRouting([("orders", "list-orders")]);
        _ = services.AddEventStoreSafeDenialQueryRouting([("parties", "list-parties")]);
        _ = services.AddEventStoreSafeDenialQueryRouting([("invoices", "list-invoices")]);

        using ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();
        IQueryRouter router = scope.ServiceProvider.GetRequiredService<IQueryRouter>();

        var outer = router.ShouldBeOfType<SafeDenialQueryRouter>();
        FieldInfo? innerField = typeof(SafeDenialQueryRouter)
            .GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
            .SingleOrDefault(f => f.FieldType == typeof(IQueryRouter));
        _ = innerField.ShouldNotBeNull();

        object? innerRouter = innerField.GetValue(outer);

        _ = innerRouter.ShouldBeOfType<StubQueryRouter>();
    }

    [Fact]
    public void AddEventStoreSafeDenialQueryRouting_CalledTwice_LastPolicyRegistrationExposesMergedRoutes() {
        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddScoped<IQueryRouter>(
            _ => new StubQueryRouter(new QueryRouterResult(Success: true, Payload: null, NotFound: false)));
        _ = services.AddEventStoreSafeDenialQueryRouting([("orders", "list-orders")]);
        _ = services.AddEventStoreSafeDenialQueryRouting([("parties", "list-parties")]);

        using ServiceProvider provider = services.BuildServiceProvider();
        ISafeDenialQueryRoutePolicy policy = provider.GetRequiredService<ISafeDenialQueryRoutePolicy>();

        var registry = policy.ShouldBeOfType<SafeDenialQueryRouteRegistry>();
        registry.Routes.ShouldContain(("orders", "list-orders"));
        registry.Routes.ShouldContain(("parties", "list-parties"));
        registry.Routes.Count.ShouldBe(2);
    }

    // Covers the ActivatorUtilities.CreateInstance resolution branch of ResolveInner: the prior
    // IQueryRouter registration here is via implementation type, not a factory or instance --
    // matching how AddEventStoreServer() really registers the base router.
    [Fact]
    public async Task AddEventStoreSafeDenialQueryRouting_PriorRegistrationViaImplementationType_ResolvesInnerRouterWithActivatorUtilities() {
        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddSingleton(new QueryRouterResult(
            Success: false,
            Payload: null,
            NotFound: false,
            ErrorMessage: QueryAdapterFailureReason.Forbidden));
        services.TryAddScoped<IQueryRouter, StubQueryRouter>();
        _ = services.AddEventStoreSafeDenialQueryRouting([("orders", "list-orders")]);

        using ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();
        IQueryRouter router = scope.ServiceProvider.GetRequiredService<IQueryRouter>();

        _ = router.ShouldBeOfType<SafeDenialQueryRouter>();

        QueryRouterResult result = await router.RouteQueryAsync(CreateQuery());

        result.Success.ShouldBeFalse();
        result.NotFound.ShouldBeTrue();
        result.ErrorMessage.ShouldBeNull();
    }

    // A custom ISafeDenialQueryRoutePolicy registered directly (not through this extension) has no
    // enumerable route list this extension can merge into -- merging would silently discard
    // whatever authorization decision the custom policy made. Must fail loudly instead.
    [Fact]
    public void AddEventStoreSafeDenialQueryRouting_ExistingCustomPolicyRegistration_ThrowsInvalidOperationException() {
        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddScoped<IQueryRouter>(
            _ => new StubQueryRouter(new QueryRouterResult(Success: true, Payload: null, NotFound: false)));
        _ = services.AddSingleton<ISafeDenialQueryRoutePolicy>(new CustomSafeDenialQueryRoutePolicy());

        _ = Should.Throw<InvalidOperationException>(
            () => services.AddEventStoreSafeDenialQueryRouting([("orders", "list-orders")]));
    }

    // Registering IEnumerable<IHostedService> at the DI-composition level (not just unit-testing
    // SafeDenialQueryRouteStartupLogger directly) proves the AddHostedService wiring line inside
    // the extension actually took effect.
    [Fact]
    public void AddEventStoreSafeDenialQueryRouting_ResolvedHostedServices_IncludesSafeDenialQueryRouteStartupLogger() {
        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddScoped<IQueryRouter>(
            _ => new StubQueryRouter(new QueryRouterResult(Success: true, Payload: null, NotFound: false)));
        _ = services.AddEventStoreSafeDenialQueryRouting([("orders", "list-orders")]);

        using ServiceProvider provider = services.BuildServiceProvider();
        IEnumerable<IHostedService> hostedServices = provider.GetRequiredService<IEnumerable<IHostedService>>();

        hostedServices.ShouldContain(service => service is SafeDenialQueryRouteStartupLogger);
    }

    [Fact]
    public void AddEventStoreSafeDenialQueryRouting_NullServices_ThrowsArgumentNullException() {
        ServiceCollection? services = null;

        Should.Throw<ArgumentNullException>(
            () => SafeDenialQueryRoutingServiceCollectionExtensions.AddEventStoreSafeDenialQueryRouting(services!, [("orders", "list-orders")]));
    }

    [Fact]
    public void AddEventStoreSafeDenialQueryRouting_NullRoutes_ThrowsArgumentNullException() {
        var services = new ServiceCollection();
        _ = services.AddScoped<IQueryRouter>(
            _ => new StubQueryRouter(new QueryRouterResult(Success: true, Payload: null, NotFound: false)));

        Should.Throw<ArgumentNullException>(
            () => services.AddEventStoreSafeDenialQueryRouting(null!));
    }
}
