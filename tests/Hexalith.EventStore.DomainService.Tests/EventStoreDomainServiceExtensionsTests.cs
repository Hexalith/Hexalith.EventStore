using System.Text.Json;

using Hexalith.EventStore.Client.Discovery;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.DomainService.Tests.Fixtures;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace Hexalith.EventStore.DomainService.Tests;

/// <summary>
/// Tier 1 coverage of the domain-service SDK host extensions and the moved router/metadata helpers.
/// </summary>
public sealed class EventStoreDomainServiceExtensionsTests {
    /// <summary>
    /// Proves the no-argument overload discovers domains in the <b>calling</b> assembly (this test
    /// assembly), not the SDK assembly — the critical <c>GetCallingAssembly</c> capture contract.
    /// </summary>
    [Fact]
    public void AddEventStoreDomainService_NoArguments_DiscoversCallingAssemblyDomains() {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();

        _ = builder.AddEventStoreDomainService();

        using ServiceProvider provider = builder.Services.BuildServiceProvider();
        DiscoveryResult discovery = provider.GetRequiredService<DiscoveryResult>();
        discovery.Aggregates.ShouldContain(a => a.DomainName == "widget");
    }

    /// <summary>
    /// Proves the explicit-assemblies overload (used when domain logic lives in a separate library,
    /// e.g. a <c>*.Server</c> project) scans exactly the supplied assembly.
    /// </summary>
    [Fact]
    public void AddEventStoreDomainService_ExplicitAssembly_DiscoversSuppliedAssemblyDomains() {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();

        _ = builder.AddEventStoreDomainService(typeof(WidgetAggregate).Assembly);

        using ServiceProvider provider = builder.Services.BuildServiceProvider();
        DiscoveryResult discovery = provider.GetRequiredService<DiscoveryResult>();
        discovery.Aggregates.ShouldContain(a => a.DomainName == "widget");
    }

    /// <summary>
    /// Proves the <c>/process</c> routing path: the request router resolves the keyed processor for the
    /// command's domain and returns the produced events.
    /// </summary>
    [Fact]
    public async Task DomainServiceRequestRouter_Process_DispatchesToKeyedProcessor() {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        _ = builder.AddEventStoreDomainService();
        using ServiceProvider provider = builder.Services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();

        DomainServiceRequest request = new(
            new CommandEnvelope(
                MessageId: Guid.NewGuid().ToString(),
                TenantId: "test-tenant",
                Domain: "widget",
                AggregateId: "widget-1",
                CommandType: nameof(CreateWidget),
                Payload: JsonSerializer.SerializeToUtf8Bytes(new CreateWidget()),
                CorrelationId: "corr-1",
                CausationId: null,
                UserId: "test-user",
                Extensions: null),
            CurrentState: null);

        DomainServiceWireResult result = await DomainServiceRequestRouter.ProcessAsync(scope.ServiceProvider, request);

        result.IsRejection.ShouldBeFalse();
        DomainServiceWireEvent @event = result.Events.ShouldHaveSingleItem();
        @event.EventTypeName.ShouldBe(typeof(WidgetCreated).FullName);
    }

    /// <summary>
    /// Proves the operational-index metadata helper surfaces the domain's command and event types.
    /// </summary>
    [Fact]
    public void AdminOperationalIndexMetadata_Create_IncludesCommandAndEventTypes() {
        DiscoveryResult discovery = new(
            [new DiscoveredDomain(typeof(WidgetAggregate), "widget", typeof(WidgetState), DomainKind.Aggregate)],
            []);

        AdminOperationalIndexMetadata.Response response = AdminOperationalIndexMetadata.Create(discovery, ["widget"]);

        AdminOperationalIndexMetadata.DomainMetadata widget = response.Domains.ShouldHaveSingleItem();
        widget.Domain.ShouldBe("widget");
        widget.CommandTypes.ShouldContain(typeof(CreateWidget).FullName!);
        widget.EventTypes.ShouldContain(typeof(WidgetCreated).FullName!);
    }

    /// <summary>
    /// Proves the operational-index metadata reports the domain's handler-served query types (A7b-1), so the
    /// gateway can later route those query types to the domain's <c>/query</c> endpoint.
    /// </summary>
    [Fact]
    public void AdminOperationalIndexMetadata_Create_IncludesHandlerQueryTypes() {
        DiscoveryResult discovery = new(
            [new DiscoveredDomain(typeof(WidgetAggregate), "widget", typeof(WidgetState), DomainKind.Aggregate)],
            []);

        AdminOperationalIndexMetadata.Response response =
            AdminOperationalIndexMetadata.Create(discovery, ["widget"], [new WidgetQueryHandler()]);

        AdminOperationalIndexMetadata.DomainMetadata widget = response.Domains.ShouldHaveSingleItem();
        widget.QueryTypes.ShouldContain("get-widget");
    }

    /// <summary>
    /// Proves <c>IDomainQueryHandler</c> implementations in the domain assembly are discovered and registered.
    /// </summary>
    [Fact]
    public void AddEventStoreDomainService_RegistersDiscoveredQueryHandlers() {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        _ = builder.AddEventStoreDomainService();
        using ServiceProvider provider = builder.Services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();

        IEnumerable<IDomainQueryHandler> handlers = scope.ServiceProvider.GetServices<IDomainQueryHandler>();
        handlers.ShouldContain(h => h.Domain == "widget" && h.QueryType == "get-widget");
    }

    /// <summary>
    /// Proves the <c>/query</c> dispatch path routes to the handler matching the envelope's domain + query type.
    /// </summary>
    [Fact]
    public async Task DomainQueryDispatcher_Execute_RoutesToMatchingHandler() {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        _ = builder.AddEventStoreDomainService();
        using ServiceProvider provider = builder.Services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();

        QueryEnvelope query = new("test-tenant", "widget", "widget-1", "get-widget", [], "corr-1", "test-user");

        QueryResult result = await DomainQueryDispatcher.ExecuteAsync(scope.ServiceProvider, query);

        result.Success.ShouldBeTrue();
        result.GetPayload().GetProperty("aggregateId").GetString().ShouldBe("widget-1");
    }

    /// <summary>
    /// Proves the dispatcher returns a failure result when no handler matches the query.
    /// </summary>
    [Fact]
    public async Task DomainQueryDispatcher_Execute_ReturnsFailure_WhenNoHandlerMatches() {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        _ = builder.AddEventStoreDomainService();
        using ServiceProvider provider = builder.Services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();

        QueryEnvelope query = new("test-tenant", "widget", "widget-1", "nonexistent-query", [], "corr-1", "test-user");

        QueryResult result = await DomainQueryDispatcher.ExecuteAsync(scope.ServiceProvider, query);

        result.Success.ShouldBeFalse();
    }
}
