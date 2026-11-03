using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;

using Dapr.Client;
using Hexalith.EventStore.Authorization;
using Hexalith.EventStore.Contracts.Authorization;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.Controllers;
using Hexalith.EventStore.ErrorHandling;
using Hexalith.EventStore.Queries;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.Server.Pipeline.Queries;
using Hexalith.EventStore.Server.Queries;
using Hexalith.EventStore.ServiceDefaults.Routing;
using Hexalith.EventStore.Testing.Http;

using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

public sealed class CatalogQuerySelectorCompatibilityTests
{
    [Theory]
    [InlineData(false, "enrolled", true)]
    [InlineData(true, "enrolled", true)]
    [InlineData(false, "absent", true)]
    [InlineData(true, "absent", true)]
    [InlineData(false, "foreign", false)]
    [InlineData(true, "foreign", false)]
    [InlineData(false, "case", false)]
    [InlineData(true, "case", false)]
    [InlineData(false, "unenrolled", false)]
    [InlineData(true, "unenrolled", false)]
    [InlineData(false, "handler-name", false)]
    [InlineData(true, "handler-name", false)]
    [InlineData(false, "message-alias", false)]
    [InlineData(true, "message-alias", false)]
    [InlineData(false, "projection", false)]
    [InlineData(true, "projection", false)]
    [InlineData(false, "scope", false)]
    [InlineData(true, "scope", false)]
    [InlineData(false, "unknown-query", false)]
    [InlineData(true, "unknown-query", false)]
    [InlineData(false, "delegated-context", false)]
    [InlineData(true, "delegated-context", false)]
    public async Task BothActualAdmissionConsumersRetainCommittedHttpReceiver(bool throughController, string scenario, bool accepted)
    {
        var declaration = Query(scenario != "unenrolled");
        if (scenario == "delegated-context") declaration = declaration with { Execution = declaration.Execution! with { RequiresDelegatedContext = true } };
        var catalog = Catalog(declaration);
        var snapshot = new CommittedRouteCatalogSnapshot(RouteCatalogCodec.Encode(catalog), RouteCatalogCodec.ComputeRootDigest(catalog), 1);
        var authority = Substitute.For<IRouteCatalogAuthority>();
        int leases = 0, effects = 0;
        authority.AcquireAsync(Arg.Any<CancellationToken>()).Returns(_ =>
        {
            leases++;
            return new RouteCatalogDispatchLease(snapshot, () => { leases--; return Task.CompletedTask; });
        });
        using var transport = new MockHttpMessageHandler(async (request, token) =>
        {
            effects++;
            request.Method.ShouldBe(HttpMethod.Post);
            request.RequestUri!.AbsolutePath.ShouldBe("/v1.0/invoke/widget-owner/method/owner-query");
            var body = await request.Content!.ReadFromJsonAsync<QueryEnvelope>(token);
            body!.TenantId.ShouldBe("tenant-a"); body.Domain.ShouldBe("widget"); body.QueryType.ShouldBe("get-widget");
            body.UserId.ShouldBe("actual-user"); body.AggregateId.ShouldBe("widget-a");
            return new HttpResponseMessage(HttpStatusCode.OK)
            { Content = JsonContent.Create(QueryResult.FromPayload(JsonSerializer.SerializeToElement(new { value = 7 }), "widgets")) };
        });
        using var http = new HttpClient(transport);
        using var dapr = new DaprClientBuilder().UseHttpEndpoint("http://127.0.0.1:3500").Build();
        var clients = Substitute.For<IHttpClientFactory>(); clients.CreateClient(DaprDomainQueryInvoker.HttpClientName).Returns(http);
        var resolver = Substitute.For<IDomainServiceResolver>();
        resolver.ResolveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new DomainServiceRegistration("foreign-app", "foreign-method", "tenant-a", "widget", "v99"));
        var invoker = new DaprDomainQueryInvoker(authority, dapr, clients, resolver, NullLogger<DaprDomainQueryInvoker>.Instance);
        var registry = Substitute.For<IDomainQueryHandlerRegistry>();
        var actorRouter = Substitute.For<IQueryRouter>();
        var router = new HandlerAwareQueryRouter(authority, actorRouter, registry, invoker, NullLogger<HandlerAwareQueryRouter>.Instance);
        string? selector = scenario switch
        {
            "absent" => null, "foreign" => "ForeignQueryActor", "case" => "legacywidgetqueryactor",
            "handler-name" => declaration.Execution!.HandlerType, "message-alias" => declaration.MessageType,
            _ => "LegacyWidgetQueryActor",
        };
        string projection = scenario == "projection" ? "foreign-projection" : "widgets";
        string tenant = scenario == "scope" ? "tenant-b" : "tenant-a";
        string queryType = scenario == "unknown-query" ? "unknown-query" : "get-widget";
        var mediator = Substitute.For<IMediator>();
        async Task InvokeAsync()
        {
            if (throughController)
            {
                mediator.Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>()).Returns(async call =>
                {
                    var result = await router.RouteQueryAsync(call.Arg<SubmitQuery>(), call.Arg<CancellationToken>());
                    return new SubmitQueryResult("correlation", result.Payload!.Value);
                });
                var controller = Controller(authority, mediator);
                var response = await controller.Submit(new SubmitQueryRequest(tenant, "widget", "widget-a", queryType,
                    projection, ProjectionActorType: selector), null, TestContext.Current.CancellationToken);
                response.ShouldBeOfType<OkObjectResult>();
            }
            else
            {
                var result = await router.RouteQueryAsync(new SubmitQuery(tenant, "widget", "widget-a", queryType, [], "correlation", "actual-user",
                    ProjectionType: projection, ProjectionActorType: selector), TestContext.Current.CancellationToken);
                result.Success.ShouldBeTrue(); result.Payload!.Value.GetProperty("value").GetInt32().ShouldBe(7);
            }
        }
        if (accepted) await InvokeAsync();
        else await Should.ThrowAsync<RouteCatalogUnavailableException>(InvokeAsync);
        effects.ShouldBe(accepted ? 1 : 0); leases.ShouldBe(0);
        if (throughController) mediator.ReceivedCalls().Count().ShouldBe(accepted || scenario == "delegated-context" ? 1 : 0);
        registry.ReceivedCalls().ShouldBeEmpty(); actorRouter.ReceivedCalls().ShouldBeEmpty(); resolver.ReceivedCalls().ShouldBeEmpty();
        var selected = snapshot.Resolve("query", "tenant-a", "widget", "get-widget");
        selected.Declaration.Execution!.TargetKind.ShouldBe("domain-service"); selected.Declaration.Execution.ActorType.ShouldBeNull();
        selected.Declaration.Execution.HandlerType.ShouldBe("Sample.WidgetQuery"); selected.Declaration.ProjectionType.ShouldBe("widgets");
        selected.Target.AppId.ShouldBe("widget-owner"); selected.Target.Method.ShouldBe("owner-query");
        if (selected.Declaration.Execution.CompatibilityProjectionActorTypes is { } frozen)
            Should.Throw<NotSupportedException>(() => ((IList<string>)frozen).Add("ForeignQueryActor"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task EnrolledSelectorNeverSubstitutesForTenantOrPermissionAuthority(bool tenantDenial)
    {
        var authority = Substitute.For<IRouteCatalogAuthority>();
        var mediator = Substitute.For<IMediator>();
        var controller = Controller(authority, mediator, tenantDenial ? TenantValidationResult.Denied("not-member") : TenantValidationResult.Allowed,
            tenantDenial ? RbacValidationResult.Allowed : RbacValidationResult.Denied("no-permission"));
        await Should.ThrowAsync<CommandAuthorizationException>(() => controller.Submit(
            new SubmitQueryRequest("tenant-a", "widget", "widget-a", "get-widget", "widgets", ProjectionActorType: "LegacyWidgetQueryActor"),
            null, TestContext.Current.CancellationToken));
        authority.ReceivedCalls().ShouldBeEmpty(); mediator.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task SelectorMutationCannotActivateAgainstOriginalIndependentOwnerObservation()
    {
        var fixture = new RouteCatalogTestFixture();
        var original = fixture.Envelope.Declarations.First(d => d.Kind == "query" && d.Execution!.TargetKind == "domain-service" && d.Execution.QueryKind == "projection");
        var changed = original with { Execution = original.Execution! with { CompatibilityProjectionActorTypes = ["ForeignQueryActor"] } };
        var envelope = RouteCatalogCodec.Seal(fixture.Envelope with
        {
            Declarations = fixture.Envelope.Declarations.Select(d => d.RouteId == original.RouteId ? changed : d).ToArray(),
            Routes = fixture.Envelope.Routes.Select(r => r.RouteId == original.RouteId ? r with { DeclarationDigest = RouteCatalogCodec.ComputeDeclarationDigest(changed) } : r).ToArray(),
        });
        var candidate = fixture.Candidate() with { CanonicalCatalog = RouteCatalogCodec.Encode(envelope), RootDigest = RouteCatalogCodec.ComputeRootDigest(envelope) };
        candidate = candidate with { TrustProof = RouteCatalogActivationBinding.ComputeDigest(candidate) };
        await Should.ThrowAsync<RouteCatalogUnavailableException>(() => fixture.ActivateAsync(candidate));
        await Should.ThrowAsync<RouteCatalogUnavailableException>(() => fixture.Authorities[0].ReadbackAsync());
    }

    private static QueriesController Controller(IRouteCatalogAuthority authority, IMediator mediator,
        TenantValidationResult? tenantResult = null, RbacValidationResult? permissionResult = null)
    {
        var tenant = Substitute.For<ITenantValidator>();
        tenant.ValidateAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<string>()).Returns(tenantResult ?? TenantValidationResult.Allowed);
        var permission = Substitute.For<IRbacValidator>();
        permission.ValidateAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<string>())
            .Returns(permissionResult ?? RbacValidationResult.Allowed);
        return new QueriesController(authority, mediator, Substitute.For<IETagService>(), tenant, permission, NullLogger<QueriesController>.Instance)
        { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "actual-user")], "fixture")) } } };
    }

    private static RouteContractDeclaration Query(bool enrolled) => new("widget-query", "query", "widget", "get-widget", "widgets", "v1", null,
        "workload", EventStoreOperationIdentifiers.DomainServiceQuery, null, [],
        new("Sample.WidgetQuery", "exact", ["get-widget"], "domain-service", "projection", []) { CompatibilityProjectionActorTypes = enrolled ? ["LegacyWidgetQueryActor"] : null });

    private static RouteCatalogEnvelope Catalog(RouteContractDeclaration query)
    {
        var foreign = Query(true) with { RouteId = "foreign-query", MessageType = "get-foreign-widget",
            Execution = query.Execution! with { Aliases = ["get-foreign-widget"], CompatibilityProjectionActorTypes = ["ForeignQueryActor"] } };
        RouteContractDeclaration[] declarations = [query, foreign];
        return RouteCatalogCodec.Seal(new(2, 1, "Development", false, declarations,
            declarations.Select(d => new RouteCatalogEntry(d.RouteId, 1, RouteCatalogCodec.ComputeDeclarationDigest(d), "widget-owner", "owner-query", "v1", d.CredentialKind, d.Operation, false, "tenant-a")).ToArray(), [], "", ""));
    }
}
