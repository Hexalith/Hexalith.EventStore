using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;

using Dapr.Client;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.Controllers;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Extensions;
using Hexalith.EventStore.Queries;
using Hexalith.EventStore.QuerySelectorFixture;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Pipeline.Queries;
using Hexalith.EventStore.Server.Projections;
using Hexalith.EventStore.Server.Queries;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.EventStore.ServiceDefaults.Routing;
using Hexalith.EventStore.Testing.Http;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

public sealed class CatalogQuerySelectorEnrollmentTests
{
    [Fact]
    public async Task CompiledOwnerCopiesEverySelectorThroughNormalCompositionActivationAndCommittedConsumption()
    {
        var fixture = new QuerySelectorCatalogFixture();
        var original = fixture.Declarations.ShouldHaveSingleItem();
        original.Execution!.CompatibilityProjectionActorTypes.ShouldBe(["LegacyWidgetQueryActor", "OlderWidgetQueryActor"]);
        string digest = RouteCatalogCodec.ComputeDeclarationDigest(original);
        SelectorFixtureCatalog.SelectorNames.Add("ForeignQueryActor");
        try
        {
            original.Execution.CompatibilityProjectionActorTypes.ShouldBe(["LegacyWidgetQueryActor", "OlderWidgetQueryActor"]);
            RouteCatalogCodec.ComputeDeclarationDigest(original).ShouldBe(digest);
            Should.Throw<NotSupportedException>(() => ((IList<string>)original.Execution.CompatibilityProjectionActorTypes!).Add("ForeignQueryActor"));
            await fixture.ActivateAsync();
            foreach (var authority in fixture.Authorities)
            {
                var readback = await authority.ReadbackAsync(TestContext.Current.CancellationToken);
                readback.Serving.ShouldBeTrue(); readback.RootDigest.ShouldBe(fixture.Candidate.RootDigest);
                readback.RequiredHosts.ShouldBe(fixture.Candidate.RequiredHosts); readback.Epoch.ShouldBe(1);
            }
            fixture.Catalog.SchemaVersion.ShouldBe(3);
            var handler = Substitute.For<IDomainQueryInvoker>();
            handler.InvokeAsync(Arg.Any<QueryEnvelope>(), Arg.Any<CancellationToken>())
                .Returns(QueryResult.FromPayload(JsonSerializer.SerializeToElement(new { id = "widget-a" }), "widgets"));
            var actor = Substitute.For<IQueryRouter>();
            var router = new HandlerAwareQueryRouter(fixture.Authorities[0], actor, Substitute.For<IDomainQueryHandlerRegistry>(), handler,
                NullLogger<HandlerAwareQueryRouter>.Instance);
            foreach (string selector in new[] { "LegacyWidgetQueryActor", "OlderWidgetQueryActor" })
                (await router.RouteQueryAsync(new("tenant-a", "widget", "widget-a", "get-widget", [], "correlation", "actual-user",
                    ProjectionType: "widgets", ProjectionActorType: selector), TestContext.Current.CancellationToken)).Success.ShouldBeTrue();
            await Should.ThrowAsync<RouteCatalogUnavailableException>(() => router.RouteQueryAsync(new("tenant-a", "widget", "widget-a", "get-widget", [], "correlation", "actual-user",
                ProjectionType: "widgets", ProjectionActorType: "ForeignQueryActor"), TestContext.Current.CancellationToken));
            handler.ReceivedCalls().Count().ShouldBe(2); actor.ReceivedCalls().ShouldBeEmpty();
            (await fixture.Store.ReadAsync(default)).State.Leases.ShouldBeEmpty();
        }
        finally { SelectorFixtureCatalog.SelectorNames.Remove("ForeignQueryActor"); }
    }

    [Fact(Timeout = 30000)]
    public async Task ActualHttpGatewayBindsValidatesAuthorizesAndMapsUnenrolledSelectorRefusalWithoutEffects()
    {
        var fixture = new QuerySelectorCatalogFixture(); await fixture.ActivateAsync();
        int effects = 0;
        var ownerServices = new ServiceCollection(); ownerServices.AddSingleton<IDomainQueryHandler, SelectorFixtureQueryHandler>();
        using var owner = ownerServices.BuildServiceProvider();
        async Task<HttpResponseMessage> Respond(HttpRequestMessage request, CancellationToken token)
        {
            request.RequestUri!.AbsolutePath.ShouldBe("/v1.0/invoke/selector-owner/method/query");
            request.Headers.GetValues(RouteCatalogInvocationHandler.RootHeader).Single().ShouldBe(fixture.Candidate.RootDigest);
            var query = await request.Content!.ReadFromJsonAsync<QueryEnvelope>(token);
            query!.TenantId.ShouldBe("tenant-a"); query.Domain.ShouldBe("widget"); query.QueryType.ShouldBe("get-widget");
            effects++;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(await DomainQueryDispatcher.ExecuteAsync(owner, query, token)) };
        }
        using var transport = new MockHttpMessageHandler(Respond);
        var transportOptions = Options.Create(new RouteCatalogHostOptions { InvocationTransportFactory = _ => new MockHttpMessageHandler(Respond) });
        using var client = new HttpClient(new RouteCatalogInvocationHandler(fixture.Authorities[0], transport, transportOptions));
        var factory = Substitute.For<IHttpClientFactory>(); factory.CreateClient(DaprDomainQueryInvoker.HttpClientName).Returns(client);
        using var dapr = new DaprClientBuilder().UseHttpEndpoint("http://127.0.0.1:3500").Build();
        var invoker = new DaprDomainQueryInvoker(fixture.Authorities[0], dapr, factory,
            Substitute.For<Hexalith.EventStore.Server.DomainServices.IDomainServiceResolver>(), NullLogger<DaprDomainQueryInvoker>.Instance);
        var actor = Substitute.For<IQueryRouter>();
        var router = new HandlerAwareQueryRouter(fixture.Authorities[0], actor, Substitute.For<IDomainQueryHandlerRegistry>(), invoker, NullLogger<HandlerAwareQueryRouter>.Instance);
        byte[] signing = RandomNumberGenerator.GetBytes(32);
        await using var app = await GatewayAsync(fixture.Authorities[0], router, signing);
        using var gateway = app.GetTestClient(); gateway.Timeout = TimeSpan.FromSeconds(5);
        gateway.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(signing, "widget", "get-widget"));
        SubmitQueryRequest Query(string selector, string tenant = "tenant-a") => new(tenant, "widget", "widget-a", "get-widget", "widgets",
            JsonSerializer.SerializeToElement(new { id = "widget-a" }), ProjectionActorType: selector);
        using var accepted = await gateway.PostAsJsonAsync("/api/v1/queries", Query("LegacyWidgetQueryActor"), TestContext.Current.CancellationToken);
        accepted.StatusCode.ShouldBe(HttpStatusCode.OK, await accepted.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)); effects.ShouldBe(1);
        using var acceptedDocument = JsonDocument.Parse(await accepted.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        acceptedDocument.RootElement.GetProperty("payload").GetProperty("Id").GetString().ShouldBe("widget-a");
        using var refused = await gateway.PostAsJsonAsync("/api/v1/queries", Query("ForeignQueryActor"), TestContext.Current.CancellationToken);
        refused.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        refused.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        using var problem = JsonDocument.Parse(await refused.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        problem.RootElement.GetProperty("detail").GetString().ShouldBe("Catalog authority cannot safely admit this operation.");
        using var invalid = await gateway.PostAsJsonAsync("/api/v1/queries", Query("bad:selector"), TestContext.Current.CancellationToken);
        invalid.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var wrongTenant = await gateway.PostAsJsonAsync("/api/v1/queries", Query("LegacyWidgetQueryActor", "tenant-b"), TestContext.Current.CancellationToken);
        wrongTenant.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        gateway.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(signing, "widget", "other-query"));
        using var permission = await gateway.PostAsJsonAsync("/api/v1/queries", Query("LegacyWidgetQueryActor"), TestContext.Current.CancellationToken);
        permission.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        effects.ShouldBe(1); actor.ReceivedCalls().ShouldBeEmpty();
        (await fixture.Store.ReadAsync(default)).State.Leases.ShouldBeEmpty();
    }

    [Theory(Timeout = 30000)]
    [InlineData(false, null, true)]
    [InlineData(false, "ProjectionActor", true)]
    [InlineData(false, "LegacyWidgetQueryActor", false)]
    [InlineData(true, null, true)]
    [InlineData(true, "ProjectionActor", true)]
    [InlineData(true, "LegacyWidgetQueryActor", false)]
    public async Task CanonicalActorBranchPreservesExactAbsentAndForeignSelectors(bool throughHttp, string? selector, bool accepted)
    {
        var fixture = new RouteCatalogTestFixture(); await fixture.ActivateAsync(fixture.Candidate());
        var actor = Substitute.For<IProjectionActorInvoker>();
        actor.InvokeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<QueryEnvelope>(), Arg.Any<CancellationToken>())
            .Returns(QueryResult.FromPayload(JsonSerializer.SerializeToElement(new { count = 7 }), "counter"));
        var lifecycle = Substitute.For<IProjectionLifecycleGateway>();
        lifecycle.ReadSnapshotAsync(Arg.Any<AggregateIdentity>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(new ProjectionLifecycleSnapshot(ProjectionLifecyclePhase.Idle, 1));
        var inner = new QueryRouter(actor, NullLogger<QueryRouter>.Instance, lifecycle, catalogAuthority: fixture.Authorities[0]);
        var http = Substitute.For<IDomainQueryInvoker>();
        var router = new HandlerAwareQueryRouter(fixture.Authorities[0], inner, Substitute.For<IDomainQueryHandlerRegistry>(), http, NullLogger<HandlerAwareQueryRouter>.Instance);
        if (throughHttp)
        {
            byte[] signing = RandomNumberGenerator.GetBytes(32);
            await using var app = await GatewayAsync(fixture.Authorities[0], router, signing);
            using var gateway = app.GetTestClient(); gateway.Timeout = TimeSpan.FromSeconds(5);
            gateway.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(signing, "counter", "get-counter-status"));
            using var response = await gateway.PostAsJsonAsync("/api/v1/queries", new SubmitQueryRequest("tenant-a", "counter", "counter-a", "get-counter-status", "counter",
                ProjectionActorType: selector), TestContext.Current.CancellationToken);
            response.StatusCode.ShouldBe(accepted ? HttpStatusCode.OK : HttpStatusCode.ServiceUnavailable);
        }
        else
        {
            var query = new SubmitQuery("tenant-a", "counter", "counter-a", "get-counter-status", [], "correlation", "actual-user", ProjectionType: "counter", ProjectionActorType: selector);
            if (accepted) (await router.RouteQueryAsync(query, TestContext.Current.CancellationToken)).Success.ShouldBeTrue();
            else await Should.ThrowAsync<RouteCatalogUnavailableException>(() => router.RouteQueryAsync(query, TestContext.Current.CancellationToken));
        }
        actor.ReceivedCalls().Count().ShouldBe(accepted ? 1 : 0); http.ReceivedCalls().ShouldBeEmpty();
        if (accepted) await actor.Received(1).InvokeAsync(Arg.Any<string>(), "ProjectionActor", Arg.Any<QueryEnvelope>(), Arg.Any<CancellationToken>());
        (await fixture.Store.ReadAsync(default)).State.Leases.ShouldBeEmpty();
    }

    private static async Task<WebApplication> GatewayAsync(IRouteCatalogAuthority authority, IQueryRouter router, byte[] signing)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.WebHost.UseTestServer(); builder.Logging.ClearProviders();
        builder.Host.UseDefaultServiceProvider(options => options.ValidateOnBuild = false);
        builder.Configuration["Authentication:JwtBearer:Issuer"] = "selector-test";
        builder.Configuration["Authentication:JwtBearer:Audience"] = "selector-test";
        builder.Configuration["Authentication:JwtBearer:SigningKey"] = Convert.ToBase64String(signing);
        builder.Services.AddEventStore(); builder.Services.RemoveAll<IHostedService>();
        builder.Services.AddDaprClient();
        builder.Services.AddSingleton(authority); builder.Services.AddSingleton(router); builder.Services.AddSingleton(Substitute.For<IETagService>());
        builder.Services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
        {
            options.MapInboundClaims = false;
            options.TokenValidationParameters = new() { ValidateIssuer = true, ValidIssuer = "selector-test", ValidateAudience = true, ValidAudience = "selector-test",
                ValidateLifetime = true, IssuerSigningKey = new SymmetricSecurityKey(signing) };
        });
        builder.Services.AddControllers().AddApplicationPart(typeof(QueriesController).Assembly)
            .ConfigureApplicationPartManager(parts => parts.FeatureProviders.Add(new CatalogV3MvcFeature()));
        var app = builder.Build();
        app.UseExceptionHandler(); app.UseAuthentication(); app.UseAuthorization(); app.MapControllers();
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }

    private static string Token(byte[] signing, string domain, string permission) => new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
    {
        Issuer = "selector-test", Audience = "selector-test", Expires = DateTime.UtcNow.AddMinutes(2),
        SigningCredentials = new(new SymmetricSecurityKey(signing), SecurityAlgorithms.HmacSha256),
        Subject = new ClaimsIdentity([new("sub", "actual-user"), new("eventstore:tenant", "tenant-a"), new("eventstore:domain", domain),
            new("eventstore:permission", permission), new(RouteCatalogSurfacePolicy.ClaimType, "ui")]),
    });
}
