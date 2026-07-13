using System.Text.Json;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.DomainService.Tests.Fixtures;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.DomainService.Tests;

public sealed class DomainProjectionHandlerCompatibilityTests {
    [Fact]
    public void AddEventStoreDomainService_RegistersNamedAsyncHandlersAsScoped() {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();

        _ = builder.AddEventStoreDomainService(typeof(WidgetAsyncProjectionHandler).Assembly);

        ServiceDescriptor descriptor = builder.Services
            .Where(service => service.ServiceType == typeof(IAsyncDomainProjectionHandler)
                && service.ImplementationType == typeof(WidgetAsyncProjectionHandler))
            .ShouldHaveSingleItem();
        descriptor.Lifetime.ShouldBe(ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddLegacyProjectionHandlerAdapter_RegistersOnlyTheExplicitPair() {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        _ = builder.AddEventStoreDomainService(typeof(WidgetProjection).Assembly);
        _ = builder.Services.AddLegacyProjectionHandlerAdapter<WidgetProjection>("widget", "widget");
        using ServiceProvider provider = builder.Services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();

        IAsyncDomainProjectionHandler adapter = scope.ServiceProvider
            .GetServices<IAsyncDomainProjectionHandler>()
            .Where(handler => handler.GetType() == typeof(LegacyDomainProjectionHandlerAdapter))
            .ShouldHaveSingleItem();

        adapter.Domain.ShouldBe("widget");
        adapter.ProjectionType.ShouldBe("widget");
    }

    [Fact]
    public async Task LegacyAdapter_ProjectAsync_ReturnsCompletedStateForMatchingRoute() {
        var adapter = new LegacyDomainProjectionHandlerAdapter(new WidgetProjection(), "widget", "widget");
        var request = new ProjectionRequest("tenant-a", "widget", "widget-1", []);

        DomainProjectionHandlerResult result = await adapter.ProjectAsync(request, "dispatch-1", CancellationToken.None);

        result.Status.ShouldBe(ProjectionDispatchStatus.Completed);
        JsonElement state = result.State.ShouldNotBeNull();
        state.GetProperty("count").GetInt32().ShouldBe(0);
        result.ReasonCode.ShouldBeNull();
    }

    [Fact]
    public async Task LegacyAdapter_ProjectAsync_ReturnsFailedForMismatchedReturnedRoute() {
        var adapter = new LegacyDomainProjectionHandlerAdapter(new WidgetProjection(), "widget", "different-route");
        var request = new ProjectionRequest("tenant-a", "widget", "widget-1", []);

        DomainProjectionHandlerResult result = await adapter.ProjectAsync(request, "dispatch-1", CancellationToken.None);

        result.Status.ShouldBe(ProjectionDispatchStatus.Failed);
        result.State.ShouldBeNull();
        result.ReasonCode.ShouldBe(ProjectionDispatchReasonCodes.MalformedOutcome);
    }

    [Fact]
    public async Task LegacyAdapter_ProjectAsync_PropagatesCancellationBeforeCallingLegacyHandler() {
        var adapter = new LegacyDomainProjectionHandlerAdapter(new WidgetProjection(), "widget", "widget");
        using var source = new CancellationTokenSource();
        source.Cancel();
        var request = new ProjectionRequest("tenant-a", "widget", "widget-1", []);

        _ = await Should.ThrowAsync<OperationCanceledException>(
            () => adapter.ProjectAsync(request, "dispatch-1", source.Token));
    }

    [Fact]
    public void NamedRouteValidator_AllowsSameDomainAndSortsDifferentProjectionTypesOrdinally() {
        IAsyncDomainProjectionHandler detail = CreateNamedHandler("widget", "widget-detail");
        IAsyncDomainProjectionHandler index = CreateNamedHandler("widget", "widget-index");

        IAsyncDomainProjectionHandler[] handlers = DomainProjectionHandlerRouteValidator.MaterializeAndValidateNamed(
            [index, detail],
            new ProjectionDispatchOptions());

        handlers.Select(handler => handler.ProjectionType).ShouldBe(["widget-detail", "widget-index"]);
    }

    [Fact]
    public void NamedRouteValidator_RejectsDuplicateCanonicalPairDeterministically() {
        IAsyncDomainProjectionHandler first = CreateNamedHandler("widget", "widget-detail");
        IAsyncDomainProjectionHandler second = CreateNamedHandler("widget", "widget-detail");

        InvalidOperationException exception = Should.Throw<InvalidOperationException>(() =>
            DomainProjectionHandlerRouteValidator.MaterializeAndValidateNamed(
                [second, first],
                new ProjectionDispatchOptions()));

        exception.Message.ShouldContain(ProjectionDispatchReasonCodes.DuplicateRoute);
        exception.Message.ShouldContain("widget/widget-detail");
    }

    [Fact]
    public void NamedRouteValidator_RejectsInvalidCanonicalRoute() {
        IAsyncDomainProjectionHandler invalid = CreateNamedHandler("Widget", "widget-detail");

        _ = Should.Throw<ArgumentException>(() =>
            DomainProjectionHandlerRouteValidator.MaterializeAndValidateNamed(
                [invalid],
                new ProjectionDispatchOptions()));
    }

    [Fact]
    public void NamedRouteValidator_RejectsOverLimitDomain() {
        IAsyncDomainProjectionHandler detail = CreateNamedHandler("widget", "widget-detail");
        IAsyncDomainProjectionHandler index = CreateNamedHandler("widget", "widget-index");
        var options = new ProjectionDispatchOptions { MaxHandlersPerDomain = 1, MaxOutcomes = 1 };

        InvalidOperationException exception = Should.Throw<InvalidOperationException>(() =>
            DomainProjectionHandlerRouteValidator.MaterializeAndValidateNamed([detail, index], options));

        exception.Message.ShouldContain("maximum 1");
        exception.Message.ShouldContain("widget");
    }

    [Fact]
    public void NamedRouteValidator_RejectsAmbiguousLegacyAdapterAndNamedHandlerPair() {
        var adapter = new LegacyDomainProjectionHandlerAdapter(new WidgetProjection(), "widget", "widget");
        IAsyncDomainProjectionHandler named = CreateNamedHandler("widget", "widget");

        InvalidOperationException exception = Should.Throw<InvalidOperationException>(() =>
            DomainProjectionHandlerRouteValidator.MaterializeAndValidateNamed(
                [adapter, named],
                new ProjectionDispatchOptions()));

        exception.Message.ShouldContain(ProjectionDispatchReasonCodes.DuplicateRoute);
        exception.Message.ShouldContain(nameof(LegacyDomainProjectionHandlerAdapter));
    }

    private static IAsyncDomainProjectionHandler CreateNamedHandler(string domain, string projectionType) {
        IAsyncDomainProjectionHandler handler = Substitute.For<IAsyncDomainProjectionHandler>();
        handler.Domain.Returns(domain);
        handler.ProjectionType.Returns(projectionType);
        return handler;
    }
}
