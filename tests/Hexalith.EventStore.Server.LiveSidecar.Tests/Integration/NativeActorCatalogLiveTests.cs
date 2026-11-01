using Dapr;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Server.Projections;
using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Dapr.Actors;
using Dapr.Actors.Client;
using Dapr.Actors.Runtime;
using Dapr.Client;
using Hexalith.EventStore;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.Extensions;
using Hexalith.EventStore.Sample.Counter;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Pipeline.Queries;
using Hexalith.EventStore.Server.Queries;
using Hexalith.EventStore.Server.Routing;
using Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.EventStore.ServiceDefaults.Routing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Shouldly;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Integration;

/// <summary>Real pinned daprd and native SDK actor callbacks under the private technical Gateway fence.</summary>
public sealed class NativeActorCatalogLiveTests
{
    [Theory]
    [InlineData("pubsub", false)]
    [InlineData("in-memory-events", false)]
    [InlineData("in-memory-events", true)]
    public async Task NativeQueryRegisteredActorsAndLifecycleKeepActualChannelCatalogAndMethodBoundaries(string component, bool declarative)
    {
        string directory = Path.Combine(Path.GetTempPath(), "native-catalog-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string appId = "native-catalog-" + Guid.NewGuid().ToString("N");
        string appToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        await using var redis = new RouteCatalogDevelopmentRedis();
        await redis.StartAsync();
        string resources = Path.Combine(directory, "components"); Directory.CreateDirectory(resources);
        async Task Component(string name, string type, bool actor)
        {
            var metadata = new List<object> { new { name = "redisHost", value = redis.Endpoint }, new { name = "redisPassword", value = "" } };
            if (actor) metadata.Add(new { name = "actorStateStore", value = "true" });
            await File.WriteAllTextAsync(Path.Combine(resources, name + ".yaml"), JsonSerializer.Serialize(new { apiVersion = "dapr.io/v1alpha1", kind = "Component", metadata = new { name, @namespace = appId }, spec = new { type, version = "v1", metadata }, scopes = new[] { appId } }));
        }
        await Component("statestore", "state.redis", true); await Component(component, "pubsub.in-memory", false);
        // This exact fixture-owned subscription supplies real native delivery on the pinned
        // broker. Programmatic discovery metadata is tested separately; its embedded-star
        // pattern is not supported by this broker and earns no wildcard-delivery claim.
        {
            await File.WriteAllTextAsync(Path.Combine(resources, "projection-subscription.yaml"), JsonSerializer.Serialize(new
            {
                apiVersion = "dapr.io/v2alpha1", kind = "Subscription", metadata = new { name = "projection-changed", @namespace = appId },
                spec = new { pubsubname = component, topic = "tenant-a.counter.projection-changed", routes = new { @default = "projections/changed" } }, scopes = new[] { appId },
            }));
        }
        string aclPath = Path.Combine(directory, "acl.json");
        await File.WriteAllTextAsync(aclPath, JsonSerializer.Serialize(new { apiVersion = "dapr.io/v1alpha1", kind = "Configuration", metadata = new { name = appId }, spec = new { accessControl = new { defaultAction = "deny", trustDomain = "public", policies = new[] { new { appId, @namespace = appId, trustDomain = "public", defaultAction = "deny", operations = new[] { new { name = "/**", httpVerb = new[] { "GET", "POST", "PUT" }, action = "allow" } } } } } } }));
        string componentPath = Path.Combine(directory, "authority.json");
        await File.WriteAllTextAsync(componentPath, JsonSerializer.Serialize(new { kind = "Component", metadata = new { name = "catalog-test", @namespace = appId }, spec = new { type = "state.redis", metadata = new[] { new { name = "keyPrefix", value = "none" } } }, scopes = new[] { appId } }));
        {
            var authority = new { apiVersion = "dapr.io/v1alpha1", kind = "Component", metadata = new { name = "catalog-test", @namespace = appId }, spec = new { type = "state.redis", version = "v1", metadata = new[] { new { name = "keyPrefix", value = "none" }, new { name = "redisHost", value = redis.Endpoint }, new { name = "redisPassword", value = "" } } }, scopes = new[] { appId } };
            await File.WriteAllTextAsync(componentPath, JsonSerializer.Serialize(authority));
            await File.WriteAllTextAsync(Path.Combine(resources, "catalog-test.yaml"), JsonSerializer.Serialize(authority));
        }
        await using var sidecar = new RouteCatalogDevelopmentSidecar(appId, resources, aclPath, appToken);
        sidecar.ConfigureNativeActorNamespace(appId);
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development", ContentRootPath = directory, Args = [] });
        builder.WebHost.UseUrls($"http://127.0.0.1:{sidecar.AppPort}");
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Dapr:DeclarativeSubscriptions"] = declarative.ToString(),
            ["EventStore:ProjectionChanges:PubSubName"] = component, ["EventStore:ProjectionChanges:Transport"] = "PubSub",
            ["EventStore:ProjectionChanges:ProvenanceAudience"] = appId, ["EventStore:ProjectionChanges:AllowedPublishers:0"] = appId,
            ["Authentication:DaprInternal:Audience"] = appId, ["Authentication:WorkloadIssuer:Workload"] = appId,
            ["APP_API_TOKEN"] = appToken, ["NAMESPACE"] = appId, ["DAPR_HTTP_ENDPOINT"] = sidecar.HttpEndpoint, ["DAPR_GRPC_ENDPOINT"] = sidecar.GrpcEndpoint,
            ["EventStore:DomainService:AppId"] = appId, ["EventStore:DomainService:ServiceVersion"] = "v1",
            ["EventStore:DataProtection:PersistToStateStore"] = "false",
            ["Authentication:JwtBearer:Issuer"] = "native-catalog", ["Authentication:JwtBearer:Audience"] = appId,
            ["Authentication:JwtBearer:SigningKey"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)), ["Authentication:JwtBearer:RequireHttpsMetadata"] = "false",
            ["EventStore:RouteCatalog:DevelopmentBootstrap:Directory"] = directory, ["EventStore:RouteCatalog:DevelopmentBootstrap:HostId"] = "native",
            ["EventStore:RouteCatalog:DevelopmentBootstrap:ReceiverProfile"] = RouteCatalogTechnicalHttpPolicy.FenceVersion,
            ["EventStore:RouteCatalog:DevelopmentBootstrap:BindingVersion"] = "2",
            ["EventStore:RouteCatalog:DevelopmentBootstrap:RequiredHostIds:0"] = "native", ["EventStore:RouteCatalog:DevelopmentBootstrap:SelectedAppIds:0"] = appId,
            ["EventStore:RouteCatalog:DevelopmentBootstrap:ComponentPath"] = componentPath, ["EventStore:RouteCatalog:DevelopmentBootstrap:AccessControlPath"] = aclPath,
            ["EventStore:RouteCatalog:DevelopmentBootstrap:DeclaredInvocations"] = JsonSerializer.Serialize(new[] { new RouteCatalogDeclaredInvocation(appId, "/**", ["GET", "POST", "PUT"]) }),
        });
        builder.Services.AddEventStore(); builder.Services.AddEventStoreServer(builder.Configuration); builder.Services.AddEventStoreDomainQueryRouting(); builder.Services.AddEventStoreWorkloadAssertionIssuer(builder.Configuration);
        builder.Services.AddControllers().AddApplicationPart(typeof(Hexalith.EventStore.Controllers.CommandsController).Assembly);
        // In-process hosting has no Aspire-injected process environment. Configure the real
        // native client after the API registration, using this owned sidecar's actual endpoints.
        using var rpcObserver = new NativeCallbackRpcObserver();
        builder.Services.RemoveAll<DaprClient>();
        builder.Services.AddSingleton(new DaprClientBuilder().UseHttpEndpoint(sidecar.HttpEndpoint).UseGrpcEndpoint(sidecar.GrpcEndpoint).UseGrpcChannelOptions(new Grpc.Net.Client.GrpcChannelOptions { HttpHandler = rpcObserver }).Build());
        builder.Services.Configure<ActorRuntimeOptions>(options => options.HttpEndpoint = sidecar.HttpEndpoint);
        var proxyFactory = new ActorProxyFactory(new ActorProxyOptions { HttpEndpoint = sidecar.HttpEndpoint });
        builder.Services.AddSingleton<IActorProxyFactory>(proxyFactory);
        builder.Services.AddSingleton<FiniteRouteCatalogReceiver>();
        builder.Services.AddSingleton<IDevelopmentNativeActorReceiver, DevelopmentNativeActorReceiver>();

        builder.Services.Configure<RouteCatalogHostOptions>(options => { options.DevelopmentFixture = true; options.Profile = "Development"; options.StateStore = "catalog-test"; options.StateKey = "native-" + appId; });
        builder.Services.AddEventStoreRouteCatalogObservation(async (provider, token) =>
        {
            using var scope = provider.CreateScope();
            var surface = provider.GetRequiredService<FiniteRouteCatalogReceiver>().Observe();
            var declaration = CompiledRouteCatalogDeclarations.Read([typeof(SampleCatalog).Assembly]).Single(declaration => RouteCatalogProjectionActorPolicy.IsQuery(declaration));
            var target = RunScopedRouteCatalogGateway.ActorQueryTargets(scope.ServiceProvider, [typeof(SampleCatalog).Assembly], appId).Single();
            return (await DevelopmentRouteCatalogInputs.CaptureAsync(builder.Configuration, appId, [declaration], [target], [], [], DevelopmentTechnicalHttpTransport.Auxiliary(surface, appId, "gateway"), token))
                with { Operations = SampleCatalog.Operations.Where(operation => operation.RouteId == declaration.RouteId).ToArray(), ReceiverSurface = surface };
        });
        await using var app = builder.Build();
        var store = app.Services.GetRequiredService<IRouteCatalogStateStore>();
        app.UseRouting();
        var notificationObserved = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        bool notificationHadHttpGeneration = false;
        int notificationCount = 0;
        var secondNotificationObserved = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var reminderObserved = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        app.Use(async (context, next) =>
        {
            bool cloudEvent = context.Request.ContentType?.StartsWith("application/cloudevents+json", StringComparison.OrdinalIgnoreCase) == true;
            await next(context);
            if (cloudEvent && context.Request.Path == "/projections/changed" && context.Response.StatusCode == StatusCodes.Status200OK)
            {
                notificationHadHttpGeneration = context.Request.Headers.ContainsKey(RouteCatalogInvocationHandler.RootHeader)
                    || context.Request.Headers.ContainsKey(RouteCatalogInvocationHandler.EpochHeader);
                if (Interlocked.Increment(ref notificationCount) == 2) secondNotificationObserved.TrySetResult(true);
                notificationObserved.TrySetResult(true);
            }
            if (context.Request.Path.Value?.EndsWith("/method/remind/native-catalog-proof", StringComparison.Ordinal) == true
                && context.Response.StatusCode == StatusCodes.Status200OK) reminderObserved.TrySetResult(true);
        });
        app.UseEventStoreHttpHostSecurity(); app.UseAuthentication(); app.UseAuthorization();
        app.UseCloudEvents();
        app.MapControllers().WithEventStoreProjectionNotifications(app.Services, declarative);
        if (!declarative) app.MapSubscribeHandler().RequireEventStoreChannel();
        app.MapActorsHandlers().WithMetadata(EventStoreActorCallbackEndpointMetadata.Instance).RequireEventStoreChannel();
        FiniteRouteCatalogReceiver.UseFiniteDevelopmentRouteCatalogReceiver(app, RunScopedRouteCatalogGateway.Operation, technical: true);
        try
        {
            // Outbound sidecar readiness is independent of the app channel. Establish it
            // before hosted-service startup writes and before any writer starts.
            await sidecar.StartAsync();
            // Observe gRPC readiness after outbound HTTP readiness;
            // only transient read-only connection startup is retried, never actor/catalog work.
            var client = app.Services.GetRequiredService<DaprClient>();
            using var grpcReady = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            grpcReady.CancelAfter(TimeSpan.FromSeconds(30));
            while (true)
            {
                sidecar.RequireRunning();
                try
                {
                    var loaded = await client.GetMetadataAsync(grpcReady.Token);
                    loaded.Id.ShouldBe(appId);
                    loaded.Components.Select(component => component.Name).ShouldContain("catalog-test");
                    loaded.Components.Select(component => component.Name).ShouldContain("statestore");
                    break;
                }
                catch (Dapr.DaprException exception) when (exception.InnerException is Grpc.Core.RpcException { StatusCode: Grpc.Core.StatusCode.Unavailable })
                { await Task.Delay(100, grpcReady.Token); }
            }
            // Establish the real persisted v2 marker in this empty, owned Redis before
            // starting any app writer or retry worker. No old writer cohort exists here.
            var deliveryStore = app.Services.GetRequiredService<IProjectionDeliveryStateStore>();
            (await deliveryStore.ReadWriterProtocolAsync(TestContext.Current.CancellationToken)).ShouldBeNull();
            var healthChecks = app.Services.GetRequiredService<HealthCheckService>();
            var beforeCutoverHealth = await healthChecks.CheckHealthAsync(TestContext.Current.CancellationToken);
            beforeCutoverHealth.Entries["projection-delivery-writer-protocol"].Status.ShouldBe(HealthStatus.Unhealthy);
            string sourceCommit = typeof(ProjectionDeliveryCutover).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion.Split('+', 2).ElementAtOrDefault(1)
                ?? throw new InvalidOperationException("The native fixture requires the deployed writer's source revision.");
            sourceCommit.Length.ShouldBe(40);
            sourceCommit.ShouldAllBe(character => char.IsAsciiHexDigitLower(character));
            var cutover = await app.Services.GetRequiredService<IProjectionDeliveryCutover>().ActivateAsync(
                new ProjectionDeliveryCutoverRequest(sourceCommit, "fresh-empty-owned-redis:" + appId,
                    WritersQuiesced: true, RetryWorkersQuiesced: true, DowngradeProhibitedAcknowledged: true), TestContext.Current.CancellationToken);
            cutover.ShouldBe(ProjectionDeliveryCutoverStatus.Activated);
            var writerProtocol = (await deliveryStore.ReadWriterProtocolAsync(TestContext.Current.CancellationToken)).ShouldNotBeNull();
            writerProtocol.IsCurrent.ShouldBeTrue(); writerProtocol.CutoverCommit.ShouldBe(sourceCommit);
            await app.StartAsync(TestContext.Current.CancellationToken);
            using var direct = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{sidecar.AppPort}") };
            // The native health mapper admits only GET, and evaluates the actual registered
            // readiness checks. Writer activation alone cannot make an uncommitted catalog ready.
            async Task RequireHealthMethodsAsync(HttpStatusCode expectedGetStatus)
            {
                foreach (string? credential in new string?[] { null, "forged" })
                {
                    using var deniedHealth = new HttpRequestMessage(HttpMethod.Get, "/healthz");
                    if (credential is not null) deniedHealth.Headers.Add("dapr-api-token", credential);
                    using var deniedResponse = await direct.SendAsync(deniedHealth, TestContext.Current.CancellationToken);
                    deniedResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
                }
                foreach (var method in new[] { HttpMethod.Get, HttpMethod.Post, HttpMethod.Put, HttpMethod.Delete, HttpMethod.Patch, HttpMethod.Head, HttpMethod.Options })
                {
                    using var health = new HttpRequestMessage(method, "/healthz"); health.Headers.Add("dapr-api-token", appToken);
                    using var healthResponse = await direct.SendAsync(health, TestContext.Current.CancellationToken);
                    healthResponse.StatusCode.ShouldBe(method == HttpMethod.Get ? expectedGetStatus : HttpStatusCode.Forbidden);
                    if (method == HttpMethod.Get)
                        (await healthResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))
                            .ShouldBe(expectedGetStatus == HttpStatusCode.OK ? "Healthy" : "Unhealthy");
                }
            }
            var unactivatedHealth = await healthChecks.CheckHealthAsync(TestContext.Current.CancellationToken);
            unactivatedHealth.Entries["route-catalog"].Status.ShouldBe(HealthStatus.Unhealthy);
            unactivatedHealth.Entries["projection-delivery-writer-protocol"].Status.ShouldBe(HealthStatus.Healthy);
            await RequireHealthMethodsAsync(HttpStatusCode.ServiceUnavailable);
            var beforeActivation = await store.ReadAsync(TestContext.Current.CancellationToken);
            beforeActivation.State.Committed.ShouldBeNull(); beforeActivation.State.Leases.ShouldBeEmpty();
            using (var discovery = new HttpRequestMessage(HttpMethod.Get, "/dapr/subscribe"))
            {
                discovery.Headers.Add("dapr-api-token", appToken);
                using var discovered = await direct.SendAsync(discovery, TestContext.Current.CancellationToken);
                discovered.StatusCode.ShouldBe(declarative ? HttpStatusCode.NotFound : HttpStatusCode.OK);
                if (!declarative)
                {
                    using var subscriptions = JsonDocument.Parse(await discovered.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
                    subscriptions.RootElement.EnumerateArray().Single(item => item.GetProperty("topic").GetString() == "*.*.projection-changed")
                        .EnumerateObject().Single(property => property.Name.Equals("pubsubname", StringComparison.OrdinalIgnoreCase)).Value.GetString().ShouldBe(component);
                }
            }
            using (var pending = new HttpRequestMessage(HttpMethod.Post, "/projections/changed"))
            {
                pending.Headers.Add("dapr-api-token", appToken); pending.Content = JsonContent.Create(new ProjectionChangedNotification("counter", "tenant-a"));
                using var refused = await direct.SendAsync(pending, TestContext.Current.CancellationToken); refused.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
            }
            using var scope = app.Services.CreateScope();
            var observer = app.Services.GetRequiredService<FiniteRouteCatalogReceiver>().Observe();
            observer.Endpoints.Count(endpoint => endpoint.NativeActor != null).ShouldBe(6);
            var observation = await DevelopmentRouteCatalogInputs.CaptureAsync(builder.Configuration, appId,
                CompiledRouteCatalogDeclarations.Read([typeof(SampleCatalog).Assembly]).Where(RouteCatalogProjectionActorPolicy.IsQuery).ToArray(), RunScopedRouteCatalogGateway.ActorQueryTargets(scope.ServiceProvider, [typeof(SampleCatalog).Assembly], appId),
                [], [], DevelopmentTechnicalHttpTransport.Auxiliary(observer, appId, "gateway"));
            observation = observation with { ReceiverSurface = observer, Operations = SampleCatalog.Operations.Where(operation => observation.Declarations.Any(declaration => declaration.RouteId == operation.RouteId)).ToArray(), AuthorityCoordinates = store.Coordinates };
            var identity = ObservedRouteCatalogHost.Identity(observation); app.Services.GetRequiredService<IOptions<RouteCatalogHostOptions>>().Value.Identity = identity;
            var envelope = RouteCatalogCodec.Seal(new RouteCatalogEnvelope(3, 1, "Development", false, observation.Declarations,
                observation.Targets.Select(target => target with { OperationContractDigest = observation.Operations.Single(operation => operation.RouteId == target.RouteId).Digest }).ToArray(), [], "", "", observation.Operations, []));
            var activation = new RouteCatalogActivation("native-actors", RouteCatalogCodec.Encode(envelope), RouteCatalogCodec.ComputeRootDigest(envelope), null, [identity], "Development-fixture", "", observation.AuxiliaryOperations);
            activation = activation with { TechnicalHttpReceivers = [new(appId, RouteCatalogTechnicalHttpPolicy.FenceVersion,
                observer.TechnicalHttp ?? throw new InvalidOperationException("The actual Gateway technical inventory is required."))
                { Invocations = observation.DeclaredInvocations }] };
            activation = activation with { TrustProof = RouteCatalogActivationBinding.ComputeDigest(activation) };
            RouteCatalogTopologyValidator.ValidateDevelopment(observation.TopologyDocuments[0], observation.TopologyDocuments[1], envelope, [identity], appId,
                observation.AuxiliaryOperations, observation.ReceiverSurface, observation.SelectedAppIds, observation.DeclaredInvocations);
            var authority = app.Services.GetRequiredService<IRouteCatalogAuthority>();
            await authority.PrepareAsync(activation); await authority.ReadyAsync(activation.AttemptId); await authority.CommitAsync(activation.AttemptId);
            var activatedHealth = await healthChecks.CheckHealthAsync(TestContext.Current.CancellationToken);
            activatedHealth.Status.ShouldBe(HealthStatus.Healthy, string.Join("; ", activatedHealth.Entries.Select(entry => entry.Key + ":" + entry.Value.Status)));
            await RequireHealthMethodsAsync(HttpStatusCode.OK);
            var etag = proxyFactory.CreateActorProxy<IETagActor>(new ActorId("counter:tenant-a"), ETagActor.ETagActorTypeName);
            (await etag.GetCurrentETagAsync()).ShouldBeNull();
            string provenance = (await app.Services.GetRequiredService<IWorkloadAssertionIssuer>().IssueAsync(new WorkloadAssertionRequest(appId,
                EventStoreWorkloadOperations.ProjectionNotify, new Dictionary<string, string>
                {
                    [EventStoreWorkloadAuthenticationDefaults.TenantBindingClaimType] = "tenant-a",
                    [EventStoreWorkloadAuthenticationDefaults.ProjectionTypeBindingClaimType] = "counter",
                    [EventStoreWorkloadAuthenticationDefaults.TopicBindingClaimType] = "tenant-a.counter.projection-changed",
                }), TestContext.Current.CancellationToken)).ShouldNotBeNull();
            foreach (string failure in new[] { "missing-channel", "invalid-channel", "missing-provenance", "wrong-tenant", "wrong-projection" })
            {
                var notification = new ProjectionChangedNotification(failure == "wrong-projection" ? "greeting" : "counter", failure == "wrong-tenant" ? "tenant-b" : "tenant-a")
                    { Provenance = failure == "missing-provenance" ? null : provenance };
                using var invalid = new HttpRequestMessage(HttpMethod.Post, "/projections/changed") { Content = JsonContent.Create(notification) };
                if (failure != "missing-channel") invalid.Headers.Add("dapr-api-token", failure == "invalid-channel" ? "invalid" : appToken);
                using var denied = await direct.SendAsync(invalid, TestContext.Current.CancellationToken);
                denied.StatusCode.ShouldBe(failure.EndsWith("channel", StringComparison.Ordinal) ? HttpStatusCode.Unauthorized : HttpStatusCode.Forbidden);
                (await etag.GetCurrentETagAsync()).ShouldBeNull();
            }
            // Observe actual sidecar subscriptions before one real SDK signed publication.
            using (var metadata = new HttpClient { BaseAddress = new Uri(sidecar.HttpEndpoint) })
            using (var ready = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken))
            {
                ready.CancelAfter(TimeSpan.FromSeconds(30));
                while (true)
                {
                    using var current = JsonDocument.Parse(await metadata.GetStringAsync("/v1.0/metadata", ready.Token));
                    if (current.RootElement.GetProperty("subscriptions").EnumerateArray().Any(item => item.EnumerateObject().Single(property => property.Name.Equals("pubsubname", StringComparison.OrdinalIgnoreCase)).Value.GetString() == component
                        && item.GetProperty("topic").GetString() == "tenant-a.counter.projection-changed")) break;
                    await Task.Delay(100, ready.Token);
                }
            }
            var notifier = app.Services.GetRequiredService<IProjectionChangeNotifier>();
            notifier.ShouldBeOfType<DaprProjectionChangeNotifier>();
            var notificationOptions = app.Services.GetRequiredService<IOptions<ProjectionChangeNotifierOptions>>().Value;
            notificationOptions.Transport.ShouldBe(ProjectionChangeTransport.PubSub); notificationOptions.PubSubName.ShouldBe(component);
            notificationOptions.ProvenanceAudience.ShouldBe(appId);
            await notifier.NotifyProjectionChangedAsync("counter", "tenant-a", cancellationToken: TestContext.Current.CancellationToken);
            rpcObserver.PublishCalls.ShouldBe(1);
            await notificationObserved.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
            notificationHadHttpGeneration.ShouldBeFalse();
            string persistedETag = (await etag.GetCurrentETagAsync()).ShouldNotBeNull();
            using (var deactivateETag = new HttpRequestMessage(HttpMethod.Delete, "/actors/ETagActor/counter:tenant-a"))
            {
                deactivateETag.Headers.Add("dapr-api-token", appToken);
                using var removed = await direct.SendAsync(deactivateETag, TestContext.Current.CancellationToken); removed.StatusCode.ShouldBe(HttpStatusCode.OK);
            }
            (await etag.GetCurrentETagAsync()).ShouldBe(persistedETag);
            string actorId = "counter:tenant-a:one";
            var actor = proxyFactory.Create(new ActorId(actorId), "ProjectionActor");
            await actor.InvokeMethodAsync(nameof(IProjectionWriteActor.UpdateProjectionAsync), ProjectionState.FromJsonElement("counter", "tenant-a", JsonSerializer.SerializeToElement(new { count = 7 })), TestContext.Current.CancellationToken);
            // The real projection update emits another signed notification. Observe its completed
            // receiver scope before asserting the final durable lease inventory.
            await secondNotificationObserved.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
            rpcObserver.PublishCalls.ShouldBe(2);
            // Re-activation must load the previously persisted projection from the real actor store.
            using (var deactivatePersisted = new HttpRequestMessage(HttpMethod.Delete, $"/actors/ProjectionActor/{actorId}"))
            {
                deactivatePersisted.Headers.Add("dapr-api-token", appToken);
                using var deactivatedPersisted = await direct.SendAsync(deactivatePersisted, TestContext.Current.CancellationToken);
                deactivatedPersisted.StatusCode.ShouldBe(HttpStatusCode.OK);
            }
            var result = await new DefaultProjectionActorInvoker(proxyFactory).InvokeAsync(actorId, "ProjectionActor", new QueryEnvelope("tenant-a", "counter", "one", "get-counter-status", [], "native-query", "native-user", "one"), TestContext.Current.CancellationToken);
            result.Success.ShouldBeTrue(); result.GetPayload().GetProperty("count").GetInt32().ShouldBe(7);
            var aggregate = proxyFactory.Create(new ActorId("tenant-a:counter:one"), "AggregateActor");
            await aggregate.InvokeMethodAsync<AggregateStreamMetadata>(nameof(IAggregateActor.GetStreamMetadataAsync), TestContext.Current.CancellationToken);
            foreach (string kind in new[] { "wrong-channel", "remote-service", "unknown-actor", "unknown-method" })
            {
                string path = kind == "unknown-actor" ? "/actors/UnknownActor/one/method/QueryAsync" : kind == "unknown-method" ? "/actors/ProjectionActor/one/method/Unknown" : "/actors/ProjectionActor/one/method/QueryAsync";
                using var request = new HttpRequestMessage(HttpMethod.Put, path); request.Headers.Add("dapr-api-token", kind == "wrong-channel" ? "forged" : appToken);
                if (kind == "remote-service") request.Headers.Add("dapr-caller-app-id", "foreign-caller");
                using var denied = await direct.SendAsync(request, TestContext.Current.CancellationToken);
                denied.StatusCode.ShouldBe(kind == "wrong-channel" ? HttpStatusCode.Unauthorized : HttpStatusCode.Forbidden);
            }
            // Schedule the actual Dapr reminder transport and observe the real SDK IRemindable callback.
            using (var scheduler = new HttpClient { BaseAddress = new Uri(sidecar.HttpEndpoint) })
            {
                string reminderPath = "/v1.0/actors/AggregateActor/tenant-a:counter:one/reminders/native-catalog-proof";
                using var registered = await scheduler.PutAsJsonAsync(reminderPath,
                    new { dueTime = "0s", period = "1s", data = Convert.ToBase64String("{}"u8) }, TestContext.Current.CancellationToken);
                registered.IsSuccessStatusCode.ShouldBeTrue();
                try { await reminderObserved.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken); }
                finally
                {
                    using var removed = await scheduler.DeleteAsync(reminderPath, TestContext.Current.CancellationToken);
                    removed.IsSuccessStatusCode.ShouldBeTrue();
                }
            }
            using var deactivate = new HttpRequestMessage(HttpMethod.Delete, $"/actors/ProjectionActor/{actorId}"); deactivate.Headers.Add("dapr-api-token", appToken);
            using var deactivated = await direct.SendAsync(deactivate, TestContext.Current.CancellationToken); deactivated.StatusCode.ShouldBe(HttpStatusCode.OK);
            (await store.ReadAsync(default)).State.Leases.ShouldBeEmpty();
        }
        finally { await sidecar.StopAsync(); await app.StopAsync(CancellationToken.None); Directory.Delete(directory, recursive: true); }
    }
}
