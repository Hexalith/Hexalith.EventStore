extern alias sampleapi;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Hexalith.Commons.UniqueIds;
using Hexalith.EventStore.Authorization;
using Hexalith.EventStore.Authentication;
using Hexalith.EventStore.Client.Registration;
using Hexalith.EventStore.Client.Routing;
using Hexalith.EventStore.Contracts.Authorization;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.Controllers;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.ErrorHandling;
using Hexalith.EventStore.Queries;
using Hexalith.EventStore.Sample.Counter;
using Hexalith.EventStore.Sample.Counter.Commands;
using Hexalith.EventStore.Sample.Greeting;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Routing;
using Hexalith.EventStore.Server.Tests.Routing;
using Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;
using Hexalith.EventStore.Server.Pipeline.Queries;
using Hexalith.EventStore.Server.Projections;
using Hexalith.EventStore.Server.Queries;
using Hexalith.EventStore.ServiceDefaults;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.EventStore.ServiceDefaults.Routing;

using MediatR;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Integration;

/// <summary>Actual gateway controller, SDK domain host and source-generated API host share one Development activation.</summary>
[Trait("Category", "LiveSidecar")]
public sealed class RouteCatalogHostedLiveTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HostedTargetsAndProtectedDaprReadbackAgreeAcrossPrepareCommitAndRollback(bool mutualTls)
    {
        string id = UniqueIdHelper.GenerateSortableUniqueStringId().ToLowerInvariant();
        await using var redis = new RouteCatalogDevelopmentRedis();
        await redis.StartAsync();
        string[] names = ["catalog-gateway-" + id, "catalog-domain-" + id, "catalog-api-" + id];
        string[] channelTokens = [.. names.Select(_ => UniqueIdHelper.GenerateSortableUniqueStringId())];
        string signingKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        string scratch = Path.Combine(Path.GetTempPath(), "eventstore-catalog-hosted-" + id);
        string resources = Path.Combine(scratch, "components");
        Directory.CreateDirectory(resources);
        string component = Path.Combine(resources, "route-catalog.yaml");
        string configuration = Path.Combine(scratch, "configuration.yaml");
        string[] hostConfigurations = [.. Enumerable.Range(0, 3).Select(index => Path.Combine(scratch, $"configuration-{index}.json"))];
        await File.WriteAllBytesAsync(component, JsonSerializer.SerializeToUtf8Bytes(new
        {
            apiVersion = "dapr.io/v1alpha1", kind = "Component", metadata = new { name = "route-catalog" },
            spec = new { type = "state.redis", version = "v1", metadata = new[]
            { new { name = "redisHost", value = redis.Endpoint }, new { name = "redisPassword", value = "" }, new { name = "keyPrefix", value = "none" } } },
            scopes = names,
        }));
        await File.WriteAllBytesAsync(configuration, JsonSerializer.SerializeToUtf8Bytes(new
        {
            apiVersion = "dapr.io/v1alpha1", kind = "Configuration", metadata = new { name = "route-catalog-development-fixture" },
            spec = new { mtls = new { enabled = mutualTls }, accessControl = new { defaultAction = "deny", trustDomain = "public" } },
        }));
        for (int index = 0; index < 3; index++)
        {
            var acl = index == 0 ? JsonSerializer.SerializeToUtf8Bytes(new
            {
                apiVersion = "dapr.io/v1alpha1", kind = "Configuration", metadata = new { name = "catalog-gateway-development" },
                spec = new { mtls = new { enabled = mutualTls }, accessControl = new { defaultAction = "deny", trustDomain = "public", policies = new[]
                {
                    new { appId = names[2], @namespace = "default", trustDomain = "public", defaultAction = "deny",
                        operations = new[] { new { name = "/api/v1/queries", httpVerb = new[] { "POST" }, action = "allow" } } },
                } } },
            }) : await File.ReadAllBytesAsync(configuration);
            await File.WriteAllBytesAsync(hostConfigurations[index], acl);
        }
        RouteCatalogDevelopmentSidecar[] sidecars = [.. names.Select((name, index) => new RouteCatalogDevelopmentSidecar(name, resources, hostConfigurations[index], channelTokens[index]))];
        await using var sentry = new RouteCatalogDevelopmentSentry();
        WebApplication?[] applications = new WebApplication?[3];
        var settings = names.Select(_ => new RouteCatalogHostOptions { Profile = "Development", DevelopmentFixture = true, StateKey = "hosted-catalog-" + id, ControlPlaneAppIds = names }).ToArray();
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        var quiescence = new ObservedQuiescenceFixture();
        var heldStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseHeld = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var heldServerCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        IProjectionActorInvoker actors = Substitute.For<IProjectionActorInvoker>();
        actors.InvokeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<QueryEnvelope>(), Arg.Any<CancellationToken>())
            .Returns(QueryResult.FromPayload(JsonSerializer.SerializeToElement(new { count = 7 }), "counter"));
        IDomainQueryHandlerRegistry registry = Substitute.For<IDomainQueryHandlerRegistry>();
        registry.SupportsQueryAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true); // stale positive for the actor route

        RouteCatalogEnvelope Catalog(long generation) => DevelopmentRouteCatalogBuilder.Build(new(generation, false, [.. SampleCatalog.Routes, .. CatalogLiveDeclarations.Routes],
            SampleCatalog.Routes.Concat(CatalogLiveDeclarations.Routes).Select(d => new RouteCompositionBinding(d.RouteId, d.Execution!.TargetKind == "projection-actor" ? names[0] : names[1],
                d.Execution.TargetKind == "projection-actor" ? "QueryAsync" : d.Kind == "command" ? "process" : d.Kind == "query" ? "query" : "project", "v1", false, "*")).ToArray(),
            SampleCatalog.Commands.Select(c =>
            {
                var policy = DevelopmentIdempotencyBindings.Bind(c, generation) with { ConsumerAppId = names[0] };
                return policy with { ContentDigest = RouteCatalogCodec.ComputeIdempotencyEntryDigest(policy) };
            }).ToArray()));
        RouteCatalogEnvelope baseline = Catalog(1);
        async Task<RouteCatalogHostObservation> Observe(int index)
        {
            var app = applications[index]!;
            using var scope = app.Services.CreateScope();
            var declarations = CompiledRouteCatalogDeclarations.Read(index == 2 ? [typeof(CounterCatalogDeclarations).Assembly] : [typeof(SampleCatalog).Assembly, typeof(CatalogLiveDeclarations).Assembly]);
            if (index == 0) scope.ServiceProvider.GetRequiredService<IQueryRouter>().ShouldBeOfType<HandlerAwareQueryRouter>();
            if (index == 1) scope.ServiceProvider.GetServices<IDomainProjectionHandler>().ShouldContain(h => h.Domain == "counter");
            if (index == 2) scope.ServiceProvider.GetRequiredService<IActionDescriptorCollectionProvider>().ActionDescriptors.Items
                .SelectMany(a => a.EndpointMetadata).OfType<RouteCatalogEndpointAttribute>().ShouldNotBeEmpty();
            byte[][] topology = [await File.ReadAllBytesAsync(component), await File.ReadAllBytesAsync(hostConfigurations[index]), sidecars[index].Arguments,
                Encoding.UTF8.GetBytes(sidecars[index].RuntimeFingerprint)];
            if (mutualTls) topology = [.. topology, await File.ReadAllBytesAsync(Path.Combine(sentry.CredentialsDirectory, "ca.crt")), await File.ReadAllBytesAsync(Path.Combine(scratch, "sentry-configuration.json"))];
            if (index == 0)
            {
                var endpoints = ((Microsoft.AspNetCore.Routing.IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints)
                    .OfType<Microsoft.AspNetCore.Routing.RouteEndpoint>().ToArray();
                endpoints.Single(endpoint => endpoint.RoutePattern.RawText?.TrimStart('/') == "api/v1/queries")
                    .Metadata.GetOrderedMetadata<Microsoft.AspNetCore.Authorization.IAuthorizeData>().ShouldNotBeEmpty();
                endpoints.Single(endpoint => endpoint.RoutePattern.RawText?.TrimStart('/') == "internal/routing/fixture-held-dispatch")
                    .Metadata.GetOrderedMetadata<Microsoft.AspNetCore.Authorization.IAuthorizeData>().ShouldContain(policy => policy.Policy ==
                        EventStoreWorkloadAuthenticationDefaults.OperationPolicy(RouteCatalogEndpointExtensions.WorkloadScheme, EventStoreOperationIdentifiers.RouteCatalogActivate));
            }
            return new(names[index], names[index], declarations, baseline.Routes.Where(r => r.AppId == names[index]).ToArray(), names,
                topology,
                JsonSerializer.SerializeToUtf8Bytes(new { issuer = "catalog-fixture", humanAudience = "catalog-human", workloadAudience = names[index], algorithm = "HS256", lifetime = 300, environment = "Development" }),
                index == 0 ? await RouteCatalogAdmissionObservation.CaptureAsync(scope.ServiceProvider, names[0]) : [],
                index == 1 ? RouteCatalogDomainObservation.Capabilities(scope.ServiceProvider) : [],
                index == 0 ? [new(names[0], "api/v1/queries", "Default", "counter"),
                    new(names[0], "internal/routing/fixture-held-dispatch", EventStoreOperationIdentifiers.RouteCatalogActivate, "counter")]
                    : index == 1 ? RouteCatalogDomainObservation.Auxiliary(scope.ServiceProvider, ((Microsoft.AspNetCore.Routing.IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints)) : [], app.Services.GetRequiredService<IRouteCatalogStateStore>().Coordinates);
        }

        string Token(string audience, string? operation = null) => new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = "catalog-fixture", Audience = audience, IssuedAt = DateTime.UtcNow, Expires = DateTime.UtcNow.AddMinutes(2),
            SigningCredentials = new(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)), SecurityAlgorithms.HmacSha256),
            Subject = new ClaimsIdentity(operation is null ? [new Claim("sub", "fixture-user"), new Claim("eventstore:tenant", "tenant-a")]
                : [new Claim("azp", audience), new Claim("eventstore:operation", operation)]),
        });

        try
        {
            if (mutualTls)
            {
                await sentry.StartAsync(scratch, redis.Docker);
                foreach (var sidecar in sidecars) sidecar.ConfigureMutualTls(sentry);
            }
            for (int index = 0; index < 3; index++)
            {
                int hostIndex = index;
                var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development", Args = [] });
                builder.Logging.ClearProviders();
                builder.WebHost.UseUrls($"http://127.0.0.1:{sidecars[index].AppPort}");
                builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["DAPR_HTTP_ENDPOINT"] = sidecars[index].HttpEndpoint, ["APP_API_TOKEN"] = channelTokens[index],
                    ["Authentication:JwtBearer:Issuer"] = "catalog-fixture", ["Authentication:JwtBearer:Audience"] = "catalog-human",
                    ["Authentication:JwtBearer:SigningKey"] = signingKey,
                    ["Authentication:RouteCatalogWorkload:Audience"] = names[index], ["Authentication:RouteCatalogWorkload:AllowedCallers:0"] = names[index],
                    ["Authentication:Workload:Audience"] = names[index], ["Authentication:Workload:AllowedCallers:0"] = names[index],
                    ["EventStore:DomainService:AppId"] = names[index], ["EventStore:DomainService:ServiceVersion"] = "v1",
                });
                if (index != 1) builder.AddServiceDefaults();
                builder.Services.AddEventStoreRouteCatalog(builder.Configuration);
                builder.Services.AddSingleton<IOptions<RouteCatalogHostOptions>>(Options.Create(settings[index]));
                builder.Services.AddEventStoreRouteCatalogObservation((_, _) => Observe(hostIndex));
                builder.Services.AddSingleton<IRouteCatalogQuiescenceVerifier>(quiescence);
                builder.Services.AddHttpClient("catalog-control").AddEventStoreDaprServiceInvocation(names[index], null);
                builder.Services.AddOptions<JwtBearerAuthenticationOptions>().BindConfiguration("Authentication:JwtBearer");
                builder.Services.AddSingleton<IConfigureOptions<JwtBearerOptions>, ConfigureJwtBearerAuthenticationOptions>();
                builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
                builder.Services.AddEventStoreHttpHostSecurity();
                if (index == 0)
                {
                    builder.Configuration["Authentication:DaprInternal:Audience"] = names[0];
                    builder.Configuration["Authentication:DaprInternal:AllowedCallers:0"] = names[0];
                    builder.Services.AddAuthentication().AddEventStoreWorkloadScheme(
                        DaprInternalAuthenticationOptions.SchemeName, "Authentication:DaprInternal");
                    builder.Services.AddEventStoreWorkloadPolicies(DaprInternalAuthenticationOptions.SchemeName,
                        [EventStoreOperationIdentifiers.GatewayOwnerAuthorityQuery]);
                }
                builder.Services.AddHttpContextAccessor();
                builder.Services.AddProblemDetails();
                builder.Services.AddExceptionHandler<RouteCatalogUnavailableExceptionHandler>();
                if (index == 0)
                {
                    RouteCatalogAdmissionFixture.Configure(builder.Services, SampleCatalog.CreateAdapters());
                    builder.Services.AddControllers().AddApplicationPart(typeof(QueriesController).Assembly)
                        .ConfigureApplicationPartManager(manager => manager.FeatureProviders.Add(new RouteCatalogFixtureControllerFilter(typeof(QueriesController))));
                    var tenant = Substitute.For<ITenantValidator>();
                    tenant.ValidateAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<string>()).Returns(TenantValidationResult.Allowed);
                    var rbac = Substitute.For<IRbacValidator>();
                    rbac.ValidateAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<string>()).Returns(RbacValidationResult.Allowed);
                    builder.Services.AddSingleton(tenant); builder.Services.AddSingleton(rbac);
                    builder.Services.AddSingleton(Substitute.For<IETagService>());
                    builder.Services.AddScoped<IQueryRouter>(provider => new HandlerAwareQueryRouter(
                        provider.GetRequiredService<IRouteCatalogAuthority>(),
                        new QueryRouter(actors, provider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<QueryRouter>>(), Substitute.For<IProjectionLifecycleGateway>(),
                            catalogAuthority: provider.GetRequiredService<IRouteCatalogAuthority>()), registry, Substitute.For<IDomainQueryInvoker>(),
                        provider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<HandlerAwareQueryRouter>>()));
                    IMediator mediator = Substitute.For<IMediator>();
                    mediator.Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>()).Returns(async call =>
                    {
                        using var scope = applications[0]!.Services.CreateScope();
                        var result = await scope.ServiceProvider.GetRequiredService<IQueryRouter>().RouteQueryAsync(call.Arg<SubmitQuery>(), call.Arg<CancellationToken>());
                        return new SubmitQueryResult(call.Arg<SubmitQuery>().CorrelationId, result.Payload!.Value, result.ProjectionType, result.Metadata);
                    });
                    builder.Services.AddSingleton(mediator);
                }
                else if (index == 1)
                {
                    builder.AddEventStoreDomainService(typeof(CounterAggregate).Assembly);
                    builder.Services.AddCounterEventSerialization(); builder.Services.AddGreetingEventSerialization();
                    builder.Services.AddScoped<IDomainQueryHandler, CatalogLiveQueryHandler>();
                }
                else
                {
                    builder.Services.AddControllers().AddApplicationPart(typeof(sampleapi::Program).Assembly);
                    builder.Services.AddTransient<sampleapi::Hexalith.EventStore.Sample.Api.Services.InboundBearerForwardingHandler>();
                    builder.Services.AddEventStoreGatewayClient(options => options.BaseAddress = new Uri(sidecars[2].HttpEndpoint))
                        .AddHttpMessageHandler<sampleapi::Hexalith.EventStore.Sample.Api.Services.InboundBearerForwardingHandler>()
                        .AddEventStoreDaprServiceInvocation(names[0], null);
                }
                var app = builder.Build();
                applications[index] = app;
                app.UseExceptionHandler(); app.UseAuthentication(); app.UseAuthorization();
                app.MapGet("/dapr/config", () => new { entities = Array.Empty<string>() })
                    .RequireAuthorization(EventStoreWorkloadAuthenticationDefaults.SidecarChannelPolicy);
                if (index == 1) app.UseEventStoreDomainService();
                else { app.MapControllers(); app.MapDefaultEndpoints(); app.UseEventStoreHttpHostSecurity(); }
                if (index == 0)
                    app.MapPost("/internal/routing/fixture-held-dispatch", async (IRouteCatalogAuthority authority) =>
                    {
                        await using (var lease = await authority.AcquireAsync())
                        {
                            heldStarted.TrySetResult();
                            await releaseHeld.Task; // Actual receiver execution deliberately survives caller cancellation.
                        }
                        heldServerCompleted.TrySetResult();
                        return Microsoft.AspNetCore.Http.Results.NoContent();
                    }).RequireAuthorization(EventStoreWorkloadAuthenticationDefaults.OperationPolicy(RouteCatalogEndpointExtensions.WorkloadScheme,
                        EventStoreOperationIdentifiers.RouteCatalogActivate));
                await app.StartAsync();
            }
            foreach (var sidecar in sidecars) await sidecar.StartAsync();
            var observations = await Task.WhenAll(Enumerable.Range(0, 3).Select(Observe));
            var identities = observations.Select(ObservedRouteCatalogHost.Identity).ToArray();
            for (int index = 0; index < 3; index++) settings[index].Identity = identities[index];
            var authorities = applications.Select(a => a!.Services.GetRequiredService<IRouteCatalogAuthority>()).ToArray();
            RouteCatalogActivation Candidate(long generation, string? predecessor)
            {
                var envelope = Catalog(generation);
                var proposal = new RouteCatalogActivation("hosted-attempt-" + generation + "-" + id, RouteCatalogCodec.Encode(envelope),
                    RouteCatalogCodec.ComputeRootDigest(envelope), predecessor, identities, "Development-fixture", "",
                    [new(names[0], "api/v1/queries", "Default", "counter"),
                     new(names[0], "internal/routing/fixture-held-dispatch", EventStoreOperationIdentifiers.RouteCatalogActivate, "counter")]);
                return proposal with { TrustProof = RouteCatalogActivationBinding.ComputeDigest(proposal) };
            }
            async Task<HttpResponseMessage> DaprGet(int index, string method, string token)
            {
                var request = new HttpRequestMessage(HttpMethod.Get, $"{sidecars[index].HttpEndpoint}/{method}");
                request.Headers.TryAddWithoutValidation(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName, token);
                return await applications[index]!.Services.GetRequiredService<IHttpClientFactory>().CreateClient("catalog-control").SendAsync(request);
            }
            async Task CheckReadbacks(string root, bool serving)
            {
                for (int index = 0; index < 3; index++)
                {
                    using var response = await DaprGet(index, RouteCatalogEndpointExtensions.ReadbackMethod, Token(names[index], EventStoreOperationIdentifiers.RouteCatalogRead));
                    response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
                    var readback = await response.Content.ReadFromJsonAsync<RouteCatalogGenerationReadback>();
                    readback!.RootDigest.ShouldBe(root); readback.Serving.ShouldBe(serving); readback.RequiredHosts.ShouldBe(identities, ignoreOrder: true);
                }
            }
            async Task Control(int index, string phase, object payload, HttpStatusCode? expected = null)
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, $"{sidecars[index].HttpEndpoint}/internal/routing/catalog/{phase}");
                request.Headers.TryAddWithoutValidation(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName,
                    Token(names[index], EventStoreOperationIdentifiers.RouteCatalogActivate));
                request.Content = JsonContent.Create(payload, payload.GetType());
                using var response = await applications[index]!.Services.GetRequiredService<IHttpClientFactory>().CreateClient("catalog-control").SendAsync(request);
                response.StatusCode.ShouldBe(expected ?? (phase == "ready" ? HttpStatusCode.OK : HttpStatusCode.NoContent), await response.Content.ReadAsStringAsync());
            }
            var first = Candidate(1, null);
            for (int index = 0; index < 3; index++)
            {
                using var unactivated = await client.GetAsync($"http://127.0.0.1:{sidecars[index].AppPort}/ready");
                unactivated.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
            }
            using (var unactivated = await Gateway("get-counter-status")) unactivated.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
            actors.ReceivedCalls().ShouldBeEmpty();
            using (var deniedBootstrap = await DaprGet(0, RouteCatalogEndpointExtensions.ReadbackMethod,
                Token(names[0], EventStoreOperationIdentifiers.DomainServiceQuery))) deniedBootstrap.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
            await Should.ThrowAsync<RouteCatalogUnavailableException>(() => DaprGet(0, "internal/routing/catalog/unknown",
                Token(names[0], EventStoreOperationIdentifiers.RouteCatalogRead)));
            await Control(0, "prepare", first);
            for (int index = 0; index < 3; index++) await Control(index, "ready", new RouteCatalogLifecycleRequest(first.AttemptId));
            await Control(0, "commit", new RouteCatalogLifecycleRequest(first.AttemptId));
            await CheckReadbacks(first.RootDigest, true);
            using (var denied = await DaprGet(0, RouteCatalogEndpointExtensions.ReadbackMethod, Token(names[0], EventStoreOperationIdentifiers.DomainServiceQuery)))
                denied.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

            async Task<HttpResponseMessage> Gateway(string query, string? actor = null)
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, $"{sidecars[0].HttpEndpoint}/v1.0/invoke/{names[0]}/method/api/v1/queries");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token("catalog-human"));
                request.Content = JsonContent.Create(new SubmitQueryRequest("tenant-a", "counter", "counter-1", query, "counter", EntityId: "counter-1", ProjectionActorType: actor));
                return await client.SendAsync(request);
            }
            using (var success = await Gateway("get-counter-status")) success.StatusCode.ShouldBe(HttpStatusCode.OK, await success.Content.ReadAsStringAsync());
            int before = actors.ReceivedCalls().Count();
            using (var unknown = await Gateway("not-declared")) unknown.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
            using (var overrideTarget = await Gateway("get-counter-status", "SecretActor")) overrideTarget.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
            actors.ReceivedCalls().Count().ShouldBe(before);

            int beforeGenerated = actors.ReceivedCalls().Count();
            using (var request = new HttpRequestMessage(HttpMethod.Get, $"http://127.0.0.1:{sidecars[2].AppPort}/api/tenant-a/counter/counter-1"))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token("catalog-human"));
                using var generated = await client.SendAsync(request);
                generated.StatusCode.ShouldBe(mutualTls ? HttpStatusCode.OK : HttpStatusCode.Forbidden, await generated.Content.ReadAsStringAsync());
            }
            if (mutualTls) actors.ReceivedCalls().Count().ShouldBeGreaterThan(beforeGenerated);
            else actors.ReceivedCalls().Count().ShouldBe(beforeGenerated);
            var command = new CommandEnvelope(UniqueIdHelper.GenerateSortableUniqueStringId(), "tenant-a", "counter", "counter-1", "increment-counter",
                JsonSerializer.SerializeToUtf8Bytes(new IncrementCounter("counter-1")), UniqueIdHelper.GenerateSortableUniqueStringId(), null, "fixture-user", null);
            async Task<HttpResponseMessage> Process(CommandEnvelope envelope)
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, $"{sidecars[1].HttpEndpoint}/v1.0/invoke/{names[1]}/method/process");
                request.Headers.TryAddWithoutValidation(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName, Token(names[1], EventStoreOperationIdentifiers.DomainServiceProcess));
                request.Headers.TryAddWithoutValidation(RouteCatalogInvocationHandler.RootHeader, first.RootDigest);
                request.Headers.TryAddWithoutValidation(RouteCatalogInvocationHandler.EpochHeader, "1");
                request.Content = JsonContent.Create(new DomainServiceRequest(envelope, null));
                return await client.SendAsync(request);
            }
            using (var processed = await Process(command)) processed.StatusCode.ShouldBe(HttpStatusCode.OK, await processed.Content.ReadAsStringAsync());
            using (var undeclared = await Process(command with { CommandType = "not-declared" })) undeclared.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
            async Task<HttpResponseMessage> DomainQuery(string type)
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, $"{sidecars[1].HttpEndpoint}/v1.0/invoke/{names[1]}/method/query");
                request.Headers.TryAddWithoutValidation(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName, Token(names[1], EventStoreOperationIdentifiers.DomainServiceQuery));
                request.Headers.TryAddWithoutValidation(RouteCatalogInvocationHandler.RootHeader, first.RootDigest);
                request.Headers.TryAddWithoutValidation(RouteCatalogInvocationHandler.EpochHeader, "1");
                request.Content = JsonContent.Create(new QueryEnvelope("tenant-a", "catalog-test", "aggregate-1", type, [], "correlation", "fixture-user"));
                return await client.SendAsync(request);
            }
            using (var computed = await DomainQuery("get-catalog-value")) computed.StatusCode.ShouldBe(HttpStatusCode.OK, await computed.Content.ReadAsStringAsync());
            using (var undeclared = await DomainQuery("not-declared")) undeclared.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
            async Task<HttpResponseMessage> Project(string domain)
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, $"{sidecars[1].HttpEndpoint}/v1.0/invoke/{names[1]}/method/project");
                request.Headers.TryAddWithoutValidation(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName, Token(names[1], EventStoreOperationIdentifiers.DomainServiceProject));
                request.Headers.TryAddWithoutValidation(RouteCatalogInvocationHandler.RootHeader, first.RootDigest);
                request.Headers.TryAddWithoutValidation(RouteCatalogInvocationHandler.EpochHeader, "1");
                request.Content = JsonContent.Create(new ProjectionRequest("tenant-a", domain, "counter-1", []));
                return await client.SendAsync(request);
            }
            using (var projected = await Project("counter"))
            {
                projected.StatusCode.ShouldBe(HttpStatusCode.OK, await projected.Content.ReadAsStringAsync());
                (await projected.Content.ReadFromJsonAsync<ProjectionResponse>())!.ProjectionType.ShouldBe("counter");
            }
            using (var undeclared = await Project("not-declared")) undeclared.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);

            var second = Candidate(2, first.RootDigest);
            using (var cancellation = new CancellationTokenSource())
            using (var heldRequest = new HttpRequestMessage(HttpMethod.Post, $"{sidecars[0].HttpEndpoint}/v1.0/invoke/{names[0]}/method/internal/routing/fixture-held-dispatch"))
            {
                heldRequest.Headers.TryAddWithoutValidation(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName,
                    Token(names[0], EventStoreOperationIdentifiers.RouteCatalogActivate));
                var guardedClient = applications[0]!.Services.GetRequiredService<IHttpClientFactory>().CreateClient("catalog-held-sender");
                Task<HttpResponseMessage> clientDispatch = guardedClient.SendAsync(heldRequest, cancellation.Token);
                await heldStarted.Task.WaitAsync(TimeSpan.FromSeconds(15));
                cancellation.Cancel();
                await Should.ThrowAsync<OperationCanceledException>(() => clientDispatch);
                var store = applications[0]!.Services.GetRequiredService<IRouteCatalogStateStore>();
                var outstanding = (await store.ReadAsync(default)).State.Leases.OrderBy(l => l.Sequence).ToArray();
                outstanding.Length.ShouldBe(2); // Sender retained plus still-running receiver.
                var ambiguousSender = outstanding[0];
                quiescence.Observe(ambiguousSender, heldServerCompleted.Task);
                Should.Throw<RouteCatalogUnavailableException>(() => quiescence.Issue(ambiguousSender));
                second = second with { AttemptId = second.AttemptId + "-retry" };
                second = second with { TrustProof = RouteCatalogActivationBinding.ComputeDigest(second) };
                await Control(0, "prepare", second);
                await Control(0, "ready", new RouteCatalogLifecycleRequest(second.AttemptId), HttpStatusCode.ServiceUnavailable);
                async Task<HttpResponseMessage> Reconcile(RouteCatalogQuiescenceRequest receipt, string operation)
                {
                    using var request = new HttpRequestMessage(HttpMethod.Post, $"{sidecars[0].HttpEndpoint}/internal/routing/catalog/reconcile");
                    request.Headers.TryAddWithoutValidation(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName, Token(names[0], operation));
                    request.Content = JsonContent.Create(receipt);
                    return await applications[0]!.Services.GetRequiredService<IHttpClientFactory>().CreateClient("catalog-control").SendAsync(request);
                }
                var forged = new RouteCatalogQuiescenceRequest(ambiguousSender.LeaseId, first.RootDigest, ambiguousSender.Epoch, new string('0', 64));
                using (var denied = await Reconcile(forged, EventStoreOperationIdentifiers.RouteCatalogRead)) denied.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
                using (var unproven = await Reconcile(forged, EventStoreOperationIdentifiers.RouteCatalogActivate)) unproven.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
                releaseHeld.SetResult();
                await heldServerCompleted.Task.WaitAsync(TimeSpan.FromSeconds(15));
                (await store.ReadAsync(default)).State.Leases.ShouldHaveSingleItem().ShouldBe(ambiguousSender);
                await Control(0, "ready", new RouteCatalogLifecycleRequest(second.AttemptId), HttpStatusCode.ServiceUnavailable);
                using (var recovered = await Reconcile(quiescence.Issue(ambiguousSender), EventStoreOperationIdentifiers.RouteCatalogActivate))
                    recovered.StatusCode.ShouldBe(HttpStatusCode.NoContent, await recovered.Content.ReadAsStringAsync());
                (await store.ReadAsync(default)).State.Leases.ShouldBeEmpty();
                await Control(0, "rollback", new RouteCatalogRollbackRequest(second.AttemptId, RouteCatalogActivationBinding.ComputeDigest(second), (await authorities[0].ReadbackAsync()).Epoch, "pending"));
            }
            second = second with { AttemptId = second.AttemptId + "-retry" };
            second = second with { TrustProof = RouteCatalogActivationBinding.ComputeDigest(second) };
            await Control(0, "prepare", second);
            await CheckReadbacks(first.RootDigest, false);
            using (var fenced = await Gateway("get-counter-status")) fenced.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
            await Control(0, "ready", new RouteCatalogLifecycleRequest(second.AttemptId));
            await sidecars[2].StopAsync();
            await Control(0, "commit", new RouteCatalogLifecycleRequest(second.AttemptId), HttpStatusCode.ServiceUnavailable);
            await Control(0, "rollback", new RouteCatalogRollbackRequest(second.AttemptId, RouteCatalogActivationBinding.ComputeDigest(second), (await authorities[0].ReadbackAsync()).Epoch, "pending"));
            await sidecars[2].StartAsync();
            await CheckReadbacks(first.RootDigest, true);
            second = second with { AttemptId = second.AttemptId + "-retry" };
            second = second with { TrustProof = RouteCatalogActivationBinding.ComputeDigest(second) };
            await Control(0, "prepare", second);
            for (int index = 0; index < 3; index++) await Control(index, "ready", new RouteCatalogLifecycleRequest(second.AttemptId));
            await Control(0, "commit", new RouteCatalogLifecycleRequest(second.AttemptId));
            await CheckReadbacks(second.RootDigest, true);
            using (var success = await Gateway("get-counter-status")) success.StatusCode.ShouldBe(HttpStatusCode.OK);
            await Control(0, "rollback", new RouteCatalogRollbackRequest(second.AttemptId, RouteCatalogActivationBinding.ComputeDigest(second), (await authorities[0].ReadbackAsync()).Epoch, "committed"));
            await CheckReadbacks(first.RootDigest, true);
            foreach (var app in applications)
            {
                var store = app!.Services.GetRequiredService<IRouteCatalogStateStore>();
                var persisted = (await store.ReadAsync(default)).State;
                persisted.Committed!.RootDigest.ShouldBe(first.RootDigest); persisted.Epoch.ShouldBe(3); persisted.Leases.ShouldBeEmpty();
            }
        }
        finally
        {
            releaseHeld.TrySetResult();
            foreach (var sidecar in sidecars) await sidecar.DisposeAsync();
            foreach (var app in applications.Where(a => a is not null)) await app!.DisposeAsync();
            await sentry.DisposeAsync();
            Directory.Delete(scratch, recursive: true);
        }
    }
}
